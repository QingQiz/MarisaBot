#pragma warning disable CS8618

using YamlDotNet.Serialization;

namespace Marisa.Plugin.Shared.Configuration;

public class PluginConfiguration
{
    public long[] Commander { get; set; }

    public TodayFortune Fortune { get; set; }

    [YamlMember(Alias = "maimai", ApplyNamingConventions = false)]
    public MaiMaiConfiguration MaiMai { get; set; }

    public ArcaeaConfiguration Arcaea { get; set; }

    public RandomPictureConfiguration RandomPicture { get; set; }

    public ChunithmConfiguration Chunithm { get; set; }

    public string FfmpegPath { get; set; }

    public OsuConfiguration Osu { get; set; }
}