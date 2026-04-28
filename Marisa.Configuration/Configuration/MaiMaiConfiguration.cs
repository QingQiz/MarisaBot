#pragma warning disable CS8618

namespace Marisa.Configuration;

public class MaiMaiConfiguration
{
    private Dictionary<string, string[]>? _version;

    public string ResourcePath { get; set; }
    public string TempPath { get; set; }
    public string BeatMapPath { get; set; }

    public MaiMaiSecret Secret { get; set; }

    public Dictionary<string, string[]> Version
    {
        get => ConfigurationManager.RequireDictionary("maimai.version", _version);
        set => _version = value;
    }

    internal Dictionary<string, string[]>? VersionRaw => _version;
}
