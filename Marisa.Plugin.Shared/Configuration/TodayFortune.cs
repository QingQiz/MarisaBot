#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Configuration;

public class TodayFortune
{
    public string[] RhythmGames { get; set; }

    public string[] Direction { get; set; }

    public string[] Position { get; set; }

    public TodayFortuneEvent[] Events { get; set; }
}