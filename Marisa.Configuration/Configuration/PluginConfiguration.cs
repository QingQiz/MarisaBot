#pragma warning disable CS8618

using YamlDotNet.Serialization;

namespace Marisa.Configuration;

public class PluginConfiguration
{
    private string[]? _chi;
    private long[]? _commander;
    private TodayFortune? _fortune;

    public string TempPath { get; set; }

    public string ResourceRoot { get; set; }

    public string DatabasePath { get; set; }

    public NapCatConfiguration NapCat { get; set; }

    public DivingFishConfiguration DivingFish { get; set; }

    public string[] Chi
    {
        get => ConfigurationManager.RequireArray("chi", _chi);
        set => _chi = value;
    }

    internal string[]? ChiRaw => _chi;

    public long[] Commander
    {
        get => ConfigurationManager.RequireArray("commander", _commander);
        set => _commander = value;
    }

    internal long[]? CommanderRaw => _commander;

    public TodayFortune Fortune
    {
        get => ConfigurationManager.RequireObject("fortune", _fortune);
        set => _fortune = value;
    }

    internal TodayFortune? FortuneRaw => _fortune;

    [YamlMember(Alias = "maimai", ApplyNamingConventions = false)]
    public MaiMaiConfiguration MaiMai { get; set; }

    public ArcaeaConfiguration Arcaea { get; set; }

    public ChunithmConfiguration Chunithm { get; set; }

    public OngekiConfiguration Ongeki { get; set; }

    public string FfmpegPath { get; set; }

    public OsuConfiguration Osu { get; set; }

    public GameConfiguration Game { get; set; }
}
