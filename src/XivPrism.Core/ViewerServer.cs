using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XivPrism.Core;
namespace XivPrism.Core;

public sealed class ViewerServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly SemaphoreSlim capacity = new(8);
    private readonly FrameStore frames;
    private readonly ViewerSessions sessions;
    private readonly string host;
    private readonly Dictionary<string, (string Mime, byte[] Data)> assets = [];
    private volatile bool disposed;
    public string? Failure { get; private set; }
    public ViewerServer(int port, bool networkEnabled, string advertisedHost, FrameStore frames, ViewerSessions sessions, IReadOnlyDictionary<string,(string Mime,byte[] Data)> assets)
    {
        this.frames = frames; this.sessions = sessions; host = advertisedHost;
        foreach (var item in assets) this.assets.Add(item.Key,item.Value);
        listener.Prefixes.Add($"http://{(networkEnabled ? "+" : "127.0.0.1")}:{port}/");
        try { listener.Start(); _ = Accept(); } catch { listener.Close(); throw; }
    }
    private async Task Accept()
    {
        while (!disposed)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception e) { if (!disposed) Failure = e.GetType().Name; return; }
            if (!capacity.Wait(0)) { context.Response.StatusCode = 503; context.Response.Close(); continue; }
            _ = Serve(context);
        }
    }
    private async Task Serve(HttpListenerContext c)
    {
        try
        {
            var req = c.Request; var response = c.Response; response.Headers["Cache-Control"] = "no-store";
            response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            response.Headers["X-Content-Type-Options"] = "nosniff"; response.Headers["Referrer-Policy"] = "no-referrer";
            if (req.Url is null || !RequestPolicy.OriginAllowed(req.Url, req.Headers["Origin"], host)) { response.StatusCode = 403; return; }
            var path = req.Url.AbsolutePath;
            if (req.HttpMethod == "GET" && assets.TryGetValue(path, out var asset)) { response.ContentType = asset.Mime; await response.OutputStream.WriteAsync(asset.Data).ConfigureAwait(false); return; }
            response.ContentType = "application/json";
            if (path == "/api/pair" && req.HttpMethod == "POST")
            {
                if (req.ContentLength64 is < 1 or > 512 || req.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true) { response.StatusCode = 400; return; }
                var buffer = new byte[(int)req.ContentLength64]; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); await req.InputStream.ReadExactlyAsync(buffer, timeout.Token).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(buffer); var code = doc.RootElement.GetProperty("code").GetString() ?? "";
                var token = sessions.Pair(code, Environment.TickCount64);
                response.StatusCode = token is null ? 403 : 200; await Write(response, JsonSerializer.Serialize(new { token, expiresInSeconds = token is null ? 0 : 43200 })).ConfigureAwait(false); return;
            }
            var auth = req.Headers["Authorization"];
            if (auth?.StartsWith("Bearer ", StringComparison.Ordinal) != true || !sessions.Authorized(auth[7..], Environment.TickCount64)) { response.StatusCode = 401; return; }
            if (path == "/api/state" && req.HttpMethod == "GET") { await Write(response, frames.Read(Environment.TickCount64)).ConfigureAwait(false); return; }
            response.StatusCode = 404;
        }
        catch (Exception) { try { c.Response.StatusCode = 400; } catch { } }
        finally { try { c.Response.Close(); } catch { } capacity.Release(); }
    }
    private static Task Write(HttpListenerResponse response, string body) => response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
    public void Dispose() { disposed = true; listener.Close(); }
}
