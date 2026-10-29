using Dalamud.Configuration;
namespace XivPrism;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public bool NetworkEnabled { get; set; }
    public string AdvertisedHost { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 41830;
}
