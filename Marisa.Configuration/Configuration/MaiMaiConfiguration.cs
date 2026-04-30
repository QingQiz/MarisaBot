#pragma warning disable CS8618

namespace Marisa.Configuration;

public class MaiMaiConfiguration
{
    public string ResourcePath { get; set; }
    public string TempPath { get; set; }
    public string BeatMapPath { get; set; }

    public MaiMaiSecret Secret { get; set; }
}
