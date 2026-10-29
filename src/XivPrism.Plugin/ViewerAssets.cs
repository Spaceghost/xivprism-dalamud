using System.Reflection;
namespace XivPrism;
internal static class ViewerAssets
{
    internal static Dictionary<string,(string Mime,byte[] Data)> Load()
    {
        var result=new Dictionary<string,(string Mime,byte[] Data)>();var assembly=Assembly.GetExecutingAssembly();
        foreach(var (name,mime) in new[]{("index.html","text/html; charset=utf-8"),("viewer.js","text/javascript; charset=utf-8"),("viewer.css","text/css; charset=utf-8")})
        {
            var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("."+name,StringComparison.Ordinal));
            using var stream=assembly.GetManifestResourceStream(resource)!;using var bytes=new MemoryStream();stream.CopyTo(bytes);
            result["/"+(name=="index.html"?"":name)]=(mime,bytes.ToArray());
        }
        return result;
    }
}
