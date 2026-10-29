using System.Text.Json;
using XivPrism.Core;
var root=Path.GetFullPath(args[0]);var control=Path.GetFullPath(args[1]);Directory.CreateDirectory(control);
var assets=new Dictionary<string,(string,byte[])>();foreach(var(name,mime)in new[]{("index.html","text/html"),("viewer.js","text/javascript"),("viewer.css","text/css")})assets["/"+(name=="index.html"?"":name)]=(mime,File.ReadAllBytes(Path.Combine(root,name)));
var sessions=new ViewerSessions(Environment.TickCount64);var frames=new FrameStore();
using var server=new ViewerServer(18787,false,"127.0.0.1",frames,sessions,assets);
File.WriteAllText(Path.Combine(control,"pair.json"),JsonSerializer.Serialize(new{code=sessions.Code(Environment.TickCount64)}));
Console.WriteLine("Prism browser fixture running with invented test data only.");
while(!File.Exists(Path.Combine(control,"stop")))
{
 if(!File.Exists(Path.Combine(control,"freeze")))frames.Publish(new(1,true,new("TEST PLAYER","ACN",20,300,346,10000,10000),null,[],[new("journal",true,true,"screen",4)],"Test Camp","Desk"),Environment.TickCount64);
 if(File.Exists(Path.Combine(control,"revoke"))){sessions.Revoke(Environment.TickCount64);File.Delete(Path.Combine(control,"revoke"));}
 await Task.Delay(200);
}
