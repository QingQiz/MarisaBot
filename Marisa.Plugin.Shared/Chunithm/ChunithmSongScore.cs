using Marisa.Plugin.Shared.Chunithm.DataFetcher;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ChunithmSongScore
{
    public sealed record ChartView(int LevelIndex, string LevelName, string Level, double? Constant, ChunithmVersusBatch.ScoreView Score);

    public sealed record PageData(ChunithmVersusBatch.SongView Song, string Nickname, IReadOnlyList<ChartView> Charts);

    /// <summary>标准谱按国服定数计算 Rating；WE 没有定数与 Rating。</summary>
    public static PageData Build(ChunithmSong song, ChunithmBattleData data)
    {
        var charts = ChunithmVersusCatalog.StandardCharts([song]).Concat(ChunithmVersusCatalog.WorldsEndCharts([song]).Values)
            .Select(chart => new ChartView(chart.LevelIndex, chart.LevelName, chart.Level, chart.LevelIndex == 5 ? null : chart.Constant,
                ChunithmVersusBatch.CreateScore(data.Scores, chart)));
        var header = new ChunithmVersusBatch.SongView(song.Id, song.Title, song.Artist, song.Genre, song.Version,
            song.BpmList.Count > 0 ? string.Join(" / ", song.BpmList[0]) : "");
        return new PageData(header, data.Nickname ?? "", charts.ToArray());
    }
}
