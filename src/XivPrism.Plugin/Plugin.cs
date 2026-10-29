using System.Collections.Concurrent;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using XivPrism.Core;
namespace XivPrism;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly ICondition conditions;
    private readonly IObjectTable objects;
    private readonly ITargetManager targets;
    private readonly IPartyList party;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly FrameStore frames = new();
    private readonly ViewerSessions sessions = new(Environment.TickCount64);
    private readonly ConcurrentQueue<(string Method, string Scene, string Profile)> requests = new();
    private readonly ICallGateProvider<string,string> call;
    private readonly ICallGateProvider<object> toggle, settings;
    private readonly ICallGateProvider<bool> ready;
    private ViewerServer? server;
    private long nextRead;
    private bool open, disposed, registered;
    private string status = "Stopped", scene = "", profile = "", cachedStatus = "{}";
    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework, IClientState client, ICondition conditions, IObjectTable objects, ITargetManager targets, IPartyList party, IPluginLog log)
    {
        this.pi = pi; this.commands = commands; this.framework = framework; this.client = client; this.conditions = conditions; this.objects = objects; this.targets = targets; this.party = party; this.log = log;
        config = pi.GetPluginConfig() as Configuration ?? new();
        config.Port = Math.Clamp(config.Port, 1024, 65535);
        call = pi.GetIpcProvider<string,string>("XivPrism.v1.Call"); toggle = pi.GetIpcProvider<object>("XivPrism.Toggle"); settings = pi.GetIpcProvider<object>("XivPrism.OpenSettings"); ready = pi.GetIpcProvider<bool>("XivPrism.Ready");
        try
        {
            registered = commands.AddHandler("/xivprism", new CommandInfo((_, arg) => { if(arg.Trim()=="status"){log.Information("Prism status: {Status}",Volatile.Read(ref cachedStatus));return;} Queue(arg.Trim() switch { "on" => "start", "off" => "stop", "revoke" => "revoke", _ => "window.open" }); }) { HelpMessage = "Prism standalone viewer: on, off, revoke; settings show its address and pairing code." });
            if (!registered) throw new InvalidOperationException("/xivprism is already registered");
            call.RegisterFunc(Call); toggle.RegisterAction(() => Queue("window.toggle")); settings.RegisterAction(Open); ready.RegisterFunc(() => !disposed);
            pi.UiBuilder.Draw += Draw; pi.UiBuilder.OpenMainUi += Open; pi.UiBuilder.OpenConfigUi += Open; framework.Update += Update; client.Logout += Logout;
            if (config.Enabled) Start();
        }
        catch { Dispose(); throw; }
    }
    private void Open() => Queue("window.open");
    private void Queue(string method, string scene = "", string profile = "") { if (!disposed && requests.Count < 32) requests.Enqueue((method, scene, profile)); }
    private void Save() => pi.SavePluginConfig(config);
    private void Start()
    {
        server?.Dispose(); server = null;
        try { server = new(config.Port,config.NetworkEnabled,config.AdvertisedHost,frames,sessions,ViewerAssets.Load()); config.Enabled = true; status = "Publishing · paired viewers only"; }
        catch (Exception e) { config.Enabled = false; status = "Publisher unavailable · " + e.GetType().Name; log.Warning("Prism listener could not start: {Type}",e.GetType().Name); }
        Save();
    }
    private void Logout(int _, int __) { frames.Publish(new(1,false,null,null,[],[],scene,profile),Environment.TickCount64); sessions.Revoke(Environment.TickCount64); }
    private string Call(string json)
    {
        if (disposed || json.Length > 2048) return "{\"ok\":false,\"error\":\"unavailable\"}";
        try
        {
            using var d = JsonDocument.Parse(json,new JsonDocumentOptions { MaxDepth = 4 }); var method = d.RootElement.GetProperty("method").GetString();
            if (method == "api.version") return JsonSerializer.Serialize(new { ok=true, result=new { version=1, module="prism", methods=new[]{"status","window.open","window.close","profile.recommend"} } });
            if (method == "status") return Volatile.Read(ref cachedStatus);
            if (method is "window.open" or "window.close") { Queue(method); return "{\"ok\":true,\"result\":{\"queued\":true}}"; }
            if (method == "profile.recommend")
            {
                var p=d.RootElement.GetProperty("params"); var s=p.GetProperty("scene").GetString()??"";var f=p.GetProperty("profile").GetString()??"";
                if (s.Length>64 || f.Length>64 || s.Any(char.IsControl) || f.Any(char.IsControl)) throw new ArgumentException();
                Queue(method,s,f);return "{\"ok\":true,\"result\":{\"queued\":true}}";
            }
            return "{\"ok\":false,\"error\":\"unsupported_method\"}";
        }
        catch { return "{\"ok\":false,\"error\":\"invalid_request\"}"; }
    }
    private void Update(IFramework _)
    {
        if(disposed)return;
        for(var i=0;i<8&&requests.TryDequeue(out var r);i++)
            switch(r.Method)
            {
                case "start": Start();break;
                case "stop": server?.Dispose();server=null;config.Enabled=false; sessions.Revoke(Environment.TickCount64);Save();status="Stopped";break;
                case "revoke": sessions.Revoke(Environment.TickCount64);break;
                case "window.open": open=true;break;case "window.close":open=false;break;case "window.toggle":open=!open;break;
                case "profile.recommend":scene=r.Scene;profile=r.Profile;break;
            }
        var now=Environment.TickCount64;if(now<nextRead)return;nextRead=now+200;
        if(server is null)
        {
            frames.Publish(new(1,false,null,null,[],[],scene,profile),now);
            Volatile.Write(ref cachedStatus,JsonSerializer.Serialize(new{ok=true,result=new{version=1,module="prism",state=new{ready=true,publishing=false,loggedIn=client.IsLoggedIn,scene,profile,window=new{open},sourceCount=0}}}));
            return;
        }
        try
        {
            Actor? player=client.IsLoggedIn&&!conditions[ConditionFlag.BetweenAreas]&&!conditions[ConditionFlag.BetweenAreas51]&&!conditions[ConditionFlag.WatchingCutscene]&&!conditions[ConditionFlag.WatchingCutscene78]&&objects.LocalPlayer is {} p?Copy(p):null;
            Actor? target=player is not null&&targets.Target is IBattleChara t?Copy(t):null;
            var members=new List<Actor>();if(player is not null) foreach(var m in party.Take(8)) members.Add(new(Clean(m.Name.ToString()),m.ClassJob.Value.Abbreviation.ToString(),m.Level,m.CurrentHP,m.MaxHP,m.CurrentMP,m.MaxMP));
            var sources=new[]{Source("journal"),Source("character"),Source("vitals"),Source("combat"),Source("chat"),Source("windows"),Source("inventory"),Source("crafting"),Piano(),Guide()};
            frames.Publish(new(1,player is not null,player,target,members,sources,scene,profile),now);
            Volatile.Write(ref cachedStatus,JsonSerializer.Serialize(new {ok=true,result=new{version=1,module="prism",state=new{ready=true,publishing=server is {Failure:null},loggedIn=player is not null,scene,profile,window=new{open},sourceCount=sources.Count(x=>x.Available)}}}));
        }
        catch(Exception e){ frames.Publish(new(1,false,null,null,[],[],scene,profile),now);status="Waiting for game data · "+e.GetType().Name; }
    }
    private static string Clean(string value)=>new(value.Where(c=>!char.IsControl(c)).Take(128).ToArray());
    private static Actor Copy(IBattleChara p)=>new(Clean(p.Name.TextValue),p.ClassJob.Value.Abbreviation.ToString(),p.Level,p.CurrentHp,p.MaxHp,p.CurrentMp,p.MaxMp);
    private SourceState Source(string module)
    {
        try
        {
            var channel="XivHud."+char.ToUpperInvariant(module[0])+module[1..]+".v1.Call";
            using var d=JsonDocument.Parse(pi.GetIpcSubscriber<string,string>(channel).InvokeFunc("{\"method\":\"status\"}"));
            var result=d.RootElement.GetProperty("result");var s=result.TryGetProperty("state",out var state)?state:result;
            var ready=s.TryGetProperty("ready",out var r)&&r.ValueKind==JsonValueKind.True;
            var mode=s.TryGetProperty("window",out var w)&&w.TryGetProperty("actualMode",out var m)?m.GetString()??"unknown":"screen";
            var count=s.TryGetProperty("acceptedQuestCount",out var q)&&q.TryGetInt32(out var number)?number:0;
            return new(module,true,ready,mode,count);
        }
        catch{return new(module,false,false,"unavailable");}
    }
    private SourceState Piano()
    {
        try
        {
            var raw=pi.GetIpcSubscriber<string>("XivPiano.NowPlaying").InvokeFunc();if(raw.Length>8192)throw new InvalidOperationException();
            using var d=JsonDocument.Parse(raw,new JsonDocumentOptions{MaxDepth=8});var p=d.RootElement;
            var playing=p.GetProperty("playing").GetBoolean();
            var title=p.TryGetProperty("title",out var t)&&t.ValueKind==JsonValueKind.String?Clean(t.GetString()!):"";
            var artist=p.TryGetProperty("artist",out var ar)&&ar.ValueKind==JsonValueKind.String?Clean(ar.GetString()!):"";
            return new("piano",true,true,playing?"playing":"paused",Info:title.Length>0?title+" · "+artist:"");
        }
        catch{return new("piano",false,false,"unavailable");}
    }
    private SourceState Guide()
    {
        try
        {
            var raw=pi.GetIpcSubscriber<string>("XivWayfinder.v1.GetState").InvokeFunc();if(raw.Length>16384)throw new InvalidOperationException();
            using var d=JsonDocument.Parse(raw,new JsonDocumentOptions{MaxDepth=8});var p=d.RootElement;
            if(p.GetProperty("version").GetInt32()!=1)throw new InvalidOperationException();
            var enabled=p.GetProperty("enabled").GetBoolean();
            var next=p.TryGetProperty("journey",out var j)&&j.TryGetProperty("next",out var n)&&n.ValueKind==JsonValueKind.String?Clean(n.GetString()!):"";
            return new("wayfinder",true,enabled,enabled?"guidance":"off",Info:next);
        }
        catch{return new("wayfinder",false,false,"unavailable");}
    }
    private void Draw()
    {
        if(!open||disposed)return;
        ImGui.SetNextWindowSize(new(590,0),ImGuiCond.FirstUseEver);
        if(ImGui.Begin("Prism · A little more room to see###XivPrism",ref open,ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped("Your standalone companion HUD. Pair a viewer to share player, target, party and module readiness. Chat contents and game controls are never sent.");
            var enabled=config.Enabled;if(ImGui.Checkbox("Enable standalone viewer",ref enabled))Queue(enabled?"start":"stop");
            var network=config.NetworkEnabled;if(ImGui.Checkbox("Allow viewers on my private network",ref network)){config.NetworkEnabled=network;Save();if(config.Enabled)Queue("start");}
            var address=config.AdvertisedHost;if(ImGui.InputText("Viewer host address",ref address,128)){config.AdvertisedHost=address.Trim();Save();}
            var url=$"http://{config.AdvertisedHost}:{config.Port}/";ImGui.TextUnformatted(url);ImGui.SameLine();if(ImGui.SmallButton("Copy address"))ImGui.SetClipboardText(url);
            ImGui.TextWrapped("Network mode uses HTTP: use a trusted private network or an encrypted tunnel. The host address must match the address entered in the viewer.");
            ImGui.Separator();ImGui.TextUnformatted("Pairing code: "+sessions.Code(Environment.TickCount64));
            if(ImGui.Button("New pairing code"))sessions.Rotate(Environment.TickCount64);ImGui.SameLine();if(ImGui.Button("Revoke all viewers"))Queue("revoke");
            ImGui.TextWrapped(status);ImGui.TextDisabled("Read-only · 5 updates per second · stale and logged-out data are hidden");
        }
        ImGui.End();
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;server?.Dispose();sessions.Revoke(Environment.TickCount64);
        framework.Update-=Update;client.Logout-=Logout;pi.UiBuilder.Draw-=Draw;pi.UiBuilder.OpenMainUi-=Open;pi.UiBuilder.OpenConfigUi-=Open;
        call.UnregisterFunc();toggle.UnregisterAction();settings.UnregisterAction();ready.UnregisterFunc();if(registered)commands.RemoveHandler("/xivprism");requests.Clear();
    }
}
