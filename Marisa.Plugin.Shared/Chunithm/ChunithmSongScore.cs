using Marisa.Plugin.Shared.Chunithm.DataFetcher;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ChunithmSongScore
{
    public sealed record ChartView(int LevelIndex, string LevelName, string Level, double? Constant, ChunithmVersusBatch.ScoreView Score);

    public sealed record PageData(ChunithmVersusBatch.SongView Song, string Nickname, IReadOnlyList<ChartView> Charts);

    /// <summary>标准谱按国服定数计算 Rating；WE 没有定数与 Rating。</summary>
    public static PageData Build(ChunithmSong song, ChunithmBattleData data)
    {
        var standard = ChunithmVersusCatalog.StandardCharts([song]).Select(chart =>
            new ChartView(chart.LevelIndex, chart.LevelName, chart.Level, chart.Constant, ChunithmVersusBatch.CreateScore(data.Scores, chart)));
        var worldsEnd = ChunithmVersusCatalog.WorldsEndCharts([song]).Select(we =>
            new ChartView(5, we.Value.LevelName, we.Value.Level, null, WorldsEndScore(we.Key)));
        var header = new ChunithmVersusBatch.SongView(song.Id, song.Title, song.Artist, song.Genre, song.Version,
            song.BpmList.Count > 0 ? string.Join(" / ", song.BpmList[0]) : "");

        return new PageData(header, data.Nickname ?? "", standard.Concat(worldsEnd).ToArray());

        ChunithmVersusBatch.ScoreView WorldsEndScore(long id) => data.Scores.TryGetValue((id, 5), out var score)
            ? new ChunithmVersusBatch.ScoreView("played", score.Achievement, ChunithmScore.GetRank(score.Achievement), null,
                score.FullCombo, score.FullChain, score.Clear ?? "")
            : new ChunithmVersusBatch.ScoreView("unplayed");
    }
}
