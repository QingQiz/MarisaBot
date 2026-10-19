namespace Marisa.Plugin.Chunithm;

using Marisa.Plugin.Shared.Chunithm;

/// <summary>汇总类指令末尾的谱面筛选后缀，可以叠加。</summary>
[Flags]
public enum ChunithmChartFilter
{
    None                  = 0,
    OverPower             = 1 << 0,
    FullCombo             = 1 << 1,
    AllJustice            = 1 << 2,
    AllJusticeCritical    = 1 << 3,
    Ss                    = 1 << 4,
    SsPlus                = 1 << 5,
    Sss                   = 1 << 6,
    SssPlus               = 1 << 7,
    NotFullCombo          = 1 << 8,
    NotAllJustice         = 1 << 9,
    NotAllJusticeCritical = 1 << 10,
    NotSs                 = 1 << 11,
    NotSsPlus             = 1 << 12,
    NotSss                = 1 << 13,
    NotSssPlus            = 1 << 14
}

public static class ChunithmChartFilterExtensions
{
    /// <summary>与成绩有关的后缀，AJC ⊂ AJ ⊂ FC，SS ⊂ SS+ ⊂ SSS ⊂ SSS+。</summary>
    private static readonly (ChunithmChartFilter Yes, ChunithmChartFilter No, Func<ChunithmScore, bool> Test)[]
        AchievementTests =
        [
            (ChunithmChartFilter.FullCombo, ChunithmChartFilter.NotFullCombo,
                score => score.IsFullCombo),
            (ChunithmChartFilter.AllJustice, ChunithmChartFilter.NotAllJustice,
                score => score.IsAllJustice),
            (ChunithmChartFilter.AllJusticeCritical, ChunithmChartFilter.NotAllJusticeCritical,
                score => score.IsAllJusticeCritical),
            (ChunithmChartFilter.Ss, ChunithmChartFilter.NotSs,
                score => score.Achievement >= ChunithmScore.RankSs),
            (ChunithmChartFilter.SsPlus, ChunithmChartFilter.NotSsPlus,
                score => score.Achievement >= ChunithmScore.RankSsp),
            (ChunithmChartFilter.Sss, ChunithmChartFilter.NotSss,
                score => score.Achievement >= ChunithmScore.RankSss),
            (ChunithmChartFilter.SssPlus, ChunithmChartFilter.NotSssPlus,
                score => score.Achievement >= ChunithmScore.RankSssp)
        ];

    private static readonly ChunithmChartFilter AchievementFilters =
        AchievementTests.Aggregate(ChunithmChartFilter.None, (all, test) => all | test.Yes | test.No);

    /// <summary>按 op 对象谱面与成绩状态筛选谱面。</summary>
    public static IEnumerable<(double constant, int i, ChunithmSong song)> FilterCharts(
        this IEnumerable<(double constant, int i, ChunithmSong song)> charts,
        ChunithmChartFilter filter,
        IReadOnlyDictionary<(long Id, int LevelIdx), ChunithmScore> scores)
    {
        if ((filter & ChunithmChartFilter.OverPower) != 0)
        {
            charts = charts.Where(x => x.i == x.song.OpObjectIndex());
        }

        if ((filter & AchievementFilters) == 0) return charts;

        return charts.Where(x => Matches(filter, scores.GetValueOrDefault((x.song.Id, x.i))));
    }

    /// <summary>每个后缀都要成立，所以取反也能和正向叠加，没游玩的谱面只有取反会保留。</summary>
    private static bool Matches(ChunithmChartFilter filter, ChunithmScore? score)
    {
        foreach (var (yes, no, test) in AchievementTests)
        {
            if ((filter & yes) != 0 && (score is null || !test(score))) return false;
            if ((filter & no) != 0 && score is not null && test(score)) return false;
        }

        return true;
    }
}
