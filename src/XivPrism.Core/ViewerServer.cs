using System.Net;
using System.Text;
using System.Text.Json;
using XivPrism.Core.Http;
namespace XivPrism.Core;

/// <summary>Paired read-only transport using the Wine-compatible XivMcp socket framing.</summary>
public sealed class ViewerServer:IDisposable
{
    private readonly HttpServer server;
    private readonly FrameStore frames;
    private readonly ViewerSessions sessions;
    private readonly string host;
    private readonly IReadOnlyDictionary<string,(string Mime,byte[] Data)> assets;
    public string? Failure{get;private set;}
    public ViewerServer(int port,bool networkEnabled,string advertisedHost,FrameStore frames,ViewerSessions sessions,IReadOnlyDictionary<string,(string Mime,byte[] Data)> assets)
    {
        this.frames=frames;this.sessions=sessions;host=advertisedHost;this.assets=assets;
        server=new(new HttpLimits{MaxConnections=8,MaxHeaderBytes=8192,MaxHeaderCount=40,MaxBodyBytes=512,HeaderTimeout=TimeSpan.FromSeconds(3),BodyTimeout=TimeSpan.FromSeconds(3),WriteTimeout=TimeSpan.FromSeconds(3),KeepAliveIdle=TimeSpan.FromSeconds(5)},Serve,(message,e)=>{if(message=="accept failed")Failure=e?.GetType().Name;});
        try{server.Start(networkEnabled?IPAddress.Any:IPAddress.Loopback,port);}catch{server.Dispose();throw;}
    }
    private async Task<bool> Serve(HttpConnection connection,HttpRequest request)
    {
        Failure=null;
        var headers=new List<KeyValuePair<string,string>>
        {
            new("Cache-Control","no-store"),new("Content-Security-Policy","default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'"),new("X-Content-Type-Options","nosniff"),new("Referrer-Policy","no-referrer")
        };
        async Task<bool> Reply(int code,string body)=>await Bytes(code,"application/json",Encoding.UTF8.GetBytes(body)).ConfigureAwait(false);
        async Task<bool> Bytes(int code,string mime,byte[] body){await connection.SendResponseAsync(request,code,headers,mime,body,close:true).ConfigureAwait(false);return false;}
        try
        {
            var authority=request.Headers.Get("Host");
            if(authority is null||!Uri.TryCreate("http://"+authority+request.Path,UriKind.Absolute,out var url)||!RequestPolicy.OriginAllowed(url,request.Headers.Get("Origin"),host))return await Reply(403,"{\"error\":\"origin_refused\"}").ConfigureAwait(false);
            if(request.Method=="GET"&&assets.TryGetValue(request.Path,out var asset))return await Bytes(200,asset.Mime,asset.Data).ConfigureAwait(false);
            if(request.Path=="/api/pair"&&request.Method=="POST")
            {
                if(request.Body.Length is <1 or >512||request.Headers.Get("Content-Type")?.StartsWith("application/json",StringComparison.OrdinalIgnoreCase)!=true)return await Reply(400,"{\"error\":\"invalid_pair_request\"}").ConfigureAwait(false);
                using var doc=JsonDocument.Parse(request.Body,new JsonDocumentOptions{MaxDepth=4});var code=doc.RootElement.GetProperty("code").GetString()??"";var token=sessions.Pair(code,Environment.TickCount64);
                return await Reply(token is null?403:200,JsonSerializer.Serialize(new{token,expiresInSeconds=token is null?0:43200})).ConfigureAwait(false);
            }
            var auth=request.Headers.Get("Authorization");
            if(auth?.StartsWith("Bearer ",StringComparison.Ordinal)!=true||!sessions.Authorized(auth[7..],Environment.TickCount64))return await Reply(401,"{\"error\":\"pairing_required\"}").ConfigureAwait(false);
            if(request.Path=="/api/state"&&request.Method=="GET")return await Reply(200,frames.Read(Environment.TickCount64)).ConfigureAwait(false);
            return await Reply(404,"{\"error\":\"not_found\"}").ConfigureAwait(false);
        }
        catch{return await Reply(400,"{\"error\":\"invalid_request\"}").ConfigureAwait(false);}
    }
    private int disposed;
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        server.StopAccepting(); _=Shutdown();
    }
    private async Task Shutdown(){try{await server.StopAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);}finally{server.Dispose();}}

}
