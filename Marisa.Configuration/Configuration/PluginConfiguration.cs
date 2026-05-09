#pragma warning disable CS8618

using YamlDotNet.Serialization;

namespace Marisa.Configuration;

public class PluginConfiguration
{
    private long[]? _commander;

    public string TempPath { get; set; }

    public string ResourceRoot { get; set; }

    public string DatabasePath { get; set; }

    public WebConfiguration Web { get; set; }

    public NapCatConfiguration NapCat { get; set; }

    public DivingFishConfiguration DivingFish { get; set; }

    public LxnsConfiguration Lxns { get; set; }

    public long[] Commander
    {
        get => ConfigurationManager.RequireArray("commander", _commander);
        set => _commander = value;
    }

    internal long[]? CommanderRaw => _commander;

    [YamlMember(Alias = "maimai", ApplyNamingConventions = false)]
    public MaiMaiConfiguration MaiMai { get; set; }

    public ArcaeaConfiguration Arcaea { get; set; }

    public ChunithmConfiguration Chunithm { get; set; }

    public OngekiConfiguration Ongeki { get; set; }

    public string FfmpegPath { get; set; }

    public OsuConfiguration Osu { get; set; }

    public GameConfiguration Game { get; set; }
}
