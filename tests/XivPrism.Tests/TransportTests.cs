using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using XivPrism.Core;
namespace XivPrism.Tests;
public sealed class TransportTests
{
    [Fact]public async Task SocketPublisherRequiresPairingAndHidesRevokedOrStaleFacts()
    {
        var ct=TestContext.Current.CancellationToken;
        var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
        var frames=new FrameStore();frames.Publish(new(1,true,new("Private player","ACN",20,10,20,30,40),null,[],[],"",""),Environment.TickCount64);
        var sessions=new ViewerSessions(Environment.TickCount64);
        using var server=new ViewerServer(port,false,"127.0.0.1",frames,sessions,new Dictionary<string,(string,byte[])>{{"/",("text/html",Encoding.UTF8.GetBytes("Prism viewer"))}});
        using var client=new HttpClient(new HttpClientHandler{UseProxy=false}){BaseAddress=new Uri($"http://127.0.0.1:{port}/"),Timeout=TimeSpan.FromSeconds(5)};
        var page=await client.GetAsync("",ct);Assert.Equal(HttpStatusCode.OK,page.StatusCode);Assert.Contains("frame-ancestors 'none'",page.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("api/state",ct)).StatusCode);
        using var hostile=new HttpRequestMessage(HttpMethod.Post,"api/pair");hostile.Headers.Add("Origin","https://attacker.example");hostile.Content=new StringContent("{}",Encoding.UTF8,"application/json");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(hostile,ct)).StatusCode);
        var code=sessions.Code(Environment.TickCount64);var pairing=await client.PostAsync("api/pair",new StringContent(JsonSerializer.Serialize(new{code}),Encoding.UTF8,"application/json"),ct);
        Assert.Equal(HttpStatusCode.OK,pairing.StatusCode);var json=JsonDocument.Parse(await pairing.Content.ReadAsStringAsync(ct));var token=json.RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        Assert.Contains("Private player",await client.GetStringAsync("api/state",ct));
        frames.Publish(new(1,false,null,null,[],[],"",""),Environment.TickCount64);
        Assert.DoesNotContain("Private player",await client.GetStringAsync("api/state",ct));
        sessions.Revoke(Environment.TickCount64);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("api/state",ct)).StatusCode);
    }
}
