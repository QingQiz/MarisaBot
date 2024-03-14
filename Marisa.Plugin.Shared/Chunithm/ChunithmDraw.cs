using Marisa.Utils;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ChunithmDraw
{
    public static async Task<string> DrawGroupedSong(
        IEnumerable<IGrouping<string, (double Constant, int LevelIdx, ChunithmSong Song)>> groupedSong,
        IReadOnlyDictionary<(long SongId, int LevelIdx), ChunithmScore> scores)
    {
        var ctx = new WebContext();
        ctx.Put("GroupedSongs", groupedSong);
        ctx.Put("Scores", scores);

        return await WebApi.ChunithmSummary(ctx.Id);
    }
}