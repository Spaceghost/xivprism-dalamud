// Transport adapted from Spaceghost/xivmcp-dalamud 27a9dd54033278b4808a7d178745dd9ca7af963a (MIT).
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XivPrism.Core.Http;

/// <summary>Minimal HTTP/1.1 server on a raw <see cref="TcpListener"/> (no http.sys, works under Wine).</summary>
internal sealed class HttpServer : IDisposable
{
    public delegate Task<bool> RequestHandler(HttpConnection connection, HttpRequest request);

    private readonly HttpLimits _limits;
    private readonly RequestHandler _handler;
    private readonly Action<string, Exception?> _log;
    private readonly ConcurrentDictionary<long, HttpConnection> _connections = new();
    private readonly ConcurrentDictionary<long, Task> _connectionTasks = new();
    private readonly CancellationTokenSource _stopCts = new();
    private TcpListener[] _listeners = [];
    private Task? _acceptTask;
    private long _nextId;

    public HttpServer(HttpLimits limits, RequestHandler handler, Action<string, Exception?> log)
    {
        _limits = limits;
        _handler = handler;
        _log = log;
    }

    public int Port { get; private set; }

    public int ConnectionCount => _connections.Count;

    public void Start(IPAddress address, int port) => Start([address], port);

    /// <summary>
    /// Binds one listener per address, all on the same port, and runs one accept loop each. With
    /// <paramref name="port"/> 0 the first bind chooses the port and the rest follow it. If any bind
    /// fails, every listener opened by this call is closed again and the exception is rethrown, so a
    /// partial bind can never leave the server listening on some of the addresses.
    /// </summary>
    public void Start(IReadOnlyList<IPAddress> addresses, int port)
    {
        if (addresses.Count == 0)
            throw new ArgumentException("At least one address is required.", nameof(addresses));

        var listeners = new List<TcpListener>(addresses.Count);
        try
        {
            foreach (var address in addresses)
            {
                var listener = new TcpListener(address, port);
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    // Allow rebinding while old connections sit in TIME_WAIT, but never two listeners on one port.
                    // .NET's ReuseAddress also sets SO_REUSEPORT on Unix, which lets a second process bind the same
                    // port and split the connections; the plugin/standalone hand-off depends on that bind failing.
                    // So: exclusive (TcpListener.Start then leaves the options alone) plus the raw SO_REUSEADDR.
                    // Not set on Windows/Wine, where SO_REUSEADDR has port-sharing semantics.
                    listener.ExclusiveAddressUse = true;
                    var (level, name) = OperatingSystem.IsLinux() ? (1, 2) : (0xffff, 4);
                    listener.Server.SetRawSocketOption(level, name, BitConverter.GetBytes(1));
                }

                if (address.AddressFamily == AddressFamily.InterNetworkV6 && !address.Equals(IPAddress.IPv6Any))
                {
                    // A specific IPv6 address must not also claim the IPv4 wildcard, or a second
                    // IPv4 listener on the same port cannot bind.
                    try
                    {
                        listener.Server.DualMode = false;
                    }
                    catch (Exception)
                    {
                        // Not supported on this stack; the bind below still tells us whether it worked.
                    }
                }

                listener.Start(64);
                listeners.Add(listener);
                if (port == 0)
                    port = ((IPEndPoint)listener.LocalEndpoint).Port;
            }
        }
        catch (Exception)
        {
            foreach (var opened in listeners)
            {
                try
                {
                    opened.Stop();
                    opened.Dispose();
                }
                catch (Exception)
                {
                    // Best effort cleanup of a failed bind.
                }
            }

            throw;
        }

        _listeners = listeners.ToArray();
        Port = port;
        _acceptTask = Task.WhenAll(_listeners.Select(AcceptLoopAsync));
    }

    /// <summary>The endpoints actually bound. Empty once stopped.</summary>
    public IReadOnlyList<IPEndPoint> BoundEndpoints
    {
        get
        {
            var listeners = _listeners;
            var result = new List<IPEndPoint>(listeners.Length);
            foreach (var listener in listeners)
            {
                try
                {
                    result.Add((IPEndPoint)listener.LocalEndpoint);
                }
                catch (Exception)
                {
                    // A listener already stopped; skip it.
                }
            }

            return result;
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener)
    {
        while (!_stopCts.IsCancellationRequested)
        {
            Socket socket;
            try
            {
                socket = await listener.AcceptSocketAsync(_stopCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException or InvalidOperationException)
            {
                if (_stopCts.IsCancellationRequested)
                    return;
                if (ex is SocketException se && se.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted)
                    continue;
                _log("accept failed", ex);
                try
                {
                    await Task.Delay(100, _stopCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                continue;
            }

            try
            {
                socket.NoDelay = true;
            }
            catch (Exception)
            {
            }

            var id = Interlocked.Increment(ref _nextId);

            // `owned` holds the connection only until its lifetime is handed to RejectBusyAsync or
            // to ProcessConnectionAsync; it is nulled at the handover so the finally never
            // double-disposes, and disposes it if anything between throws.
            HttpConnection? owned = null;
            try
            {
                owned = new HttpConnection(socket, _limits, id);
                var connection = owned;
                if (_connections.Count >= _limits.MaxConnections || _stopCts.IsCancellationRequested)
                {
                    owned = null;
                    _ = RejectBusyAsync(connection);
                    continue;
                }

                _connections[id] = connection;
                owned = null;
                var task = Task.Run(() => ProcessConnectionAsync(connection));
                _connectionTasks[id] = task;
                if (task.IsCompleted)
                    _connectionTasks.TryRemove(id, out _);
            }
            finally
            {
                owned?.Dispose();
            }
        }
    }

    private static async Task RejectBusyAsync(HttpConnection connection)
    {
        try
        {
            connection.StartPump();
            await connection.SendErrorAsync(null, 503, "Too many connections", close: true).ConfigureAwait(false);
            await connection.CloseAfterResponseAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            connection.Abort();
        }
        finally
        {
            connection.Dispose();
        }
    }

    private async Task ProcessConnectionAsync(HttpConnection connection)
    {
        try
        {
            connection.StartPump();
            var first = true;
            while (!connection.IsClosed && !_stopCts.IsCancellationRequested)
            {
                var (request, headError) = await connection.ReadRequestHeadAsync(first).ConfigureAwait(false);
                first = false;
                if (request is null)
                {
                    if (headError is { } e && !connection.IsClosed)
                    {
                        await connection.SendErrorAsync(null, e.Status, e.Message, close: true).ConfigureAwait(false);
                        await connection.CloseAfterResponseAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    else
                    {
                        connection.CloseGracefully();
                    }

                    return;
                }

                var bodyError = await connection.ReadBodyAsync(request).ConfigureAwait(false);
                if (bodyError is { } be)
                {
                    if (be.Status != 0 && !connection.IsClosed)
                    {
                        await connection.SendErrorAsync(request, be.Status, be.Message, close: true).ConfigureAwait(false);
                        await connection.CloseAfterResponseAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    else
                    {
                        connection.Abort();
                    }

                    return;
                }

                bool keepAlive;
                try
                {
                    keepAlive = await _handler(connection, request).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    connection.Abort();
                    return;
                }
                catch (Exception ex)
                {
                    _log($"unhandled error serving {request.Method} {request.Path}", ex);
                    if (!connection.ResponseStarted && !connection.IsClosed)
                    {
                        await connection.SendErrorAsync(request, 500, "Internal server error", close: true).ConfigureAwait(false);
                        await connection.CloseAfterResponseAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    else
                    {
                        connection.Abort();
                    }

                    return;
                }

                if (!keepAlive || !request.KeepAlive)
                {
                    await connection.CloseAfterResponseAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (Exception ex)
        {
            _log("connection error", ex);
        }
        finally
        {
            connection.Abort();
            _connections.TryRemove(connection.Id, out _);
            _connectionTasks.TryRemove(connection.Id, out _);
            connection.Dispose();
        }
    }

    /// <summary>Stops accepting and releases the listening port immediately. Existing connections keep running.</summary>
    public void StopAccepting()
    {
        try
        {
            _stopCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        foreach (var listener in Interlocked.Exchange(ref _listeners, []))
        {
            try
            {
                listener.Stop();
                listener.Dispose();
            }
            catch (Exception ex)
            {
                _log("listener stop failed", ex);
            }
        }
    }

    /// <summary>Stops accepting, releases the port immediately, resets every connection and waits briefly for handlers.</summary>
    public async Task StopAsync(TimeSpan grace)
    {
        StopAccepting();

        foreach (var connection in _connections.Values)
            connection.Abort();

        var tasks = _connectionTasks.Values.ToArray();
        if (_acceptTask is not null)
            tasks = [.. tasks, _acceptTask];
        try
        {
            await Task.WhenAll(tasks).WaitAsync(grace).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Timeouts or handler faults during shutdown are not actionable.
        }
    }

    /// <summary>
    /// Releases the stop source and anything the connection table still holds. Call after
    /// <see cref="StopAsync"/>: the accept loop and the connection tasks have ended by then, so
    /// nothing reads <c>_stopCts.Token</c> any more.
    /// </summary>
    public void Dispose()
    {
        StopAccepting();
        foreach (var connection in _connections.Values)
            connection.Dispose();
        _connections.Clear();
        _connectionTasks.Clear();
        _stopCts.Dispose();
    }
}

