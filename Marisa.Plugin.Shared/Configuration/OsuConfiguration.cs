#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Configuration;

public class OsuConfiguration
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    
    public string TempPath { get; set; }
    public string ResourcePath { get; set; }
}