namespace Marisa.Plugin.Shared.Chunithm;

using Marisa.Plugin.Shared.Versus;

public static class ChunithmDraw
{
    public static Task<string> DrawVersus(ChunithmVersusBatch.SingleData data) =>
        WebApi.ChunithmVersus(new WebContext(new { chuVersus = data }).Id);

    public static Task<string> DrawVersusBatch(ChunithmVersusBatch batch, int page) =>
        WebApi.ChunithmVersusBatch(new WebContext(new { chuVersusBatch = batch.GetPage(page) }).Id);

    public static Task<string> DrawVersusMulti(ChunithmVersusMulti result, int round, IReadOnlyList<VersusStanding> standings) =>
        WebApi.ChunithmVersusMulti(new WebContext(new { chuVersusMulti = result.GetSingle(round, standings) }).Id);

    public static Task<string> DrawVersusMultiBatch(ChunithmVersusMulti result, int page, int round, IReadOnlyList<VersusStanding> standings) =>
        WebApi.ChunithmVersusMultiBatch(new WebContext(new { chuVersusMultiBatch = result.GetPage(page, round, standings) }).Id);

    public static async Task<string> DrawGroupedSong(
        IEnumerable<IGrouping<string, (double Constant, int LevelIdx, ChunithmSong Song)>> groupedSong,
        IReadOnlyDictionary<(long SongId, int LevelIdx), ChunithmScore> scores,
        string sort = "")
    {
        var ctx = new WebContext();
        ctx.Put("GroupedSongs", groupedSong.Select(x => new { x.Key, x }).ToArray());
        ctx.Put("Scores", scores);

        return await WebApi.ChunithmSummary(ctx.Id, sort);
    }
}
