using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace XivPrism.Core;

public sealed record Actor(string Name, string Job, int Level, uint Hp, uint MaxHp, uint Mp, uint MaxMp);
public sealed record SourceState(string Name, bool Available, bool Ready, string Mode, int Count = 0, string Info = "");
public sealed record PrismFrame(int Version, bool LoggedIn, Actor? Player, Actor? Target, IReadOnlyList<Actor> Party, IReadOnlyList<SourceState> Sources, string Scene, string Profile);

public sealed class FrameStore
{
    private readonly object gate = new();
    private string json = "{}";
    private long observed = long.MinValue;
    private bool live;
    public void Publish(PrismFrame value, long now)
    {
        if (value.Version != 1 || value.Party.Count > 8 || value.Sources.Count > 16) throw new ArgumentException("Invalid snapshot bounds");
        var text = JsonSerializer.Serialize(value, Json);
        if (Encoding.UTF8.GetByteCount(text) > 32768) throw new ArgumentException("Snapshot too large");
        lock (gate) { json = text; observed = now; live = value.LoggedIn && value.Player is not null; }
    }
    public string Read(long now)
    {
        lock (gate)
        {
            var age = observed == long.MinValue || now < observed ? long.MaxValue : now - observed;
            return age > 2000 || !live ? JsonSerializer.Serialize(new { version = 1, live = false, ageMs = age, state = (object?)null }, Json)
                : "{\"version\":1,\"live\":true,\"ageMs\":" + age + ",\"state\":" + json + "}";
        }
    }
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
}

/// <summary>Short-lived code pairing; only hashes of viewer capabilities are retained.</summary>
public sealed class ViewerSessions
{
    private readonly object gate = new();
    private readonly Dictionary<string, long> sessions = [];
    private string code = NewCode();
    private long codeUntil, attemptWindow;
    private int attempts;
    public ViewerSessions(long now) { codeUntil = now + 300000; attemptWindow = now; }
    private static string NewCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public string Code(long now) { lock (gate) return now <= codeUntil ? code : "Expired · rotate code"; }
    public void Rotate(long now) { lock (gate) { code = NewCode(); codeUntil = now + 300000; } }
    public string? Pair(string input, long now)
    {
        lock (gate)
        {
            if (now < attemptWindow || now - attemptWindow >= 60000) { attemptWindow = now; attempts = 0; }
            if (++attempts > 5 || now > codeUntil || input.Length != 6 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(code))) return null;
            foreach (var key in sessions.Where(x => x.Value < now).Select(x => x.Key).ToArray()) sessions.Remove(key);
            if (sessions.Count >= 8) return null;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); sessions[Hash(token)] = now + 43200000;
            code = NewCode(); codeUntil = now + 300000; return token;
        }
    }
    public bool Authorized(string? token, long now)
    {
        if (token is null || token.Length != 64) return false;
        lock (gate) return sessions.TryGetValue(Hash(token), out var until) && now <= until;
    }
    public void Revoke(long now) { lock (gate) { sessions.Clear(); attempts = 0; attemptWindow = now; Rotate(now); } }
}

public static class RequestPolicy
{
    public static bool OriginAllowed(Uri request, string? origin, string advertisedHost)
    {
        var host = request.Host;
        if (host != "127.0.0.1" && host != "localhost" && !string.Equals(host, advertisedHost, StringComparison.OrdinalIgnoreCase)) return false;
        return origin is null || string.Equals(origin.TrimEnd('/'), request.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }
}
