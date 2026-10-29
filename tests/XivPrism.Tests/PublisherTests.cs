using System.Text.Json;
using XivPrism.Core;
namespace XivPrism.Tests;
public sealed class PublisherTests
{
    private static PrismFrame Frame(bool live=true)=>new(1,live,live?new("Test adventurer","ACN",20,300,346,10000,10000):null,null,[],[],"Campfire","Desk");
    [Fact]public void NoSnapshotAndStaleOrLoggedOutSnapshotsNeverExposeGameFacts()
    {
        var store=new FrameStore();Assert.DoesNotContain("Test adventurer",store.Read(100));store.Publish(Frame(),1000);
        Assert.Contains("Test adventurer",store.Read(1200));Assert.DoesNotContain("Test adventurer",store.Read(3001));Assert.DoesNotContain("Test adventurer",store.Read(999));
        store.Publish(Frame(false),4000);Assert.DoesNotContain("Test adventurer",store.Read(4000));
    }
    [Fact]public void SchemaAndSnapshotSizeAreBounded()
    {
        var store=new FrameStore();Assert.Throws<ArgumentException>(()=>store.Publish(Frame() with{Version=2},0));
        Assert.Throws<ArgumentException>(()=>store.Publish(Frame() with{Party=Enumerable.Repeat(Frame().Player!,9).ToArray()},0));
        Assert.Throws<ArgumentException>(()=>store.Publish(Frame() with{Scene=new string('a',40000)},0));
    }
    [Fact]public void PairingNeedsAValidCodeAndIssuesOnlyRevocableCapabilities()
    {
        var sessions=new ViewerSessions(1000);var code=sessions.Code(1000);Assert.Null(sessions.Pair("000000",1100));var token=sessions.Pair(code,1200);
        Assert.NotNull(token);Assert.True(sessions.Authorized(token,1300));Assert.False(sessions.Authorized("invalid",1300));Assert.Null(sessions.Pair(code,1400));
        sessions.Revoke(1500);Assert.False(sessions.Authorized(token,1501));
    }
    [Fact]public void CodesExpireAndAttemptsAreGloballyLimited()
    {
        var sessions=new ViewerSessions(0);var code=sessions.Code(0);for(var i=0;i<5;i++)Assert.Null(sessions.Pair("wrong",1000));
        Assert.Null(sessions.Pair(code,1001));Assert.NotNull(sessions.Pair(code,61001));sessions.Rotate(100000);Assert.Null(sessions.Pair(sessions.Code(100000),400001));
    }
    [Fact]public void ViewerSessionsExpireAndCannotGrowWithoutBound()
    {
        var sessions=new ViewerSessions(0);string? first=null;
        for(var i=0;i<8;i++){var now=i*61000;var token=sessions.Pair(sessions.Code(now),now);Assert.NotNull(token);first??=token;}
        Assert.Null(sessions.Pair(sessions.Code(9*61000),9*61000));Assert.False(sessions.Authorized(first,43200001));
    }
    [Theory]
    [InlineData("http://127.0.0.1:41830/","http://127.0.0.1:41830",true)]
    [InlineData("http://127.0.0.1:41830/","https://attacker.example",false)]
    [InlineData("http://attacker.example:41830/","http://attacker.example:41830",false)]
    [InlineData("http://100.100.110.16:41830/","http://100.100.110.16:41830",true)]
    public void OriginsAndHostNamesMustMatchTheConfiguredPublisher(string request,string origin,bool allowed)=>Assert.Equal(allowed,RequestPolicy.OriginAllowed(new Uri(request),origin,"100.100.110.16"));
}
