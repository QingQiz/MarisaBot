using Flurl.Http;
using Marisa.Plugin.Shared.Osu;
using Marisa.Plugin.Shared.Osu.Drawer;
using Marisa.Utils;
using Microsoft.AspNetCore.Mvc;
using Image = SixLabors.ImageSharp.Image;

namespace Marisa.StartUp.Controllers;

[ApiController]
[Route("Api/[controller]/[action]")]
public class Osu : Controller
{
    [HttpGet]
    public object PerformanceCalculator(
        long beatmapsetId, string beatmapChecksum, long beatmapId, int modeInt, [FromQuery] string[] mods, double acc, int maxCombo, int cMax, int c300,
        int c200, int c100,
        int c50, int cMiss, long score)
    {
        return new
        {
            StarRating = Plugin.Shared.Osu.PerformanceCalculator.GetStarRating(beatmapsetId, beatmapChecksum, beatmapId, modeInt, mods),
            Pp = Plugin.Shared.Osu.PerformanceCalculator.GetPerformancePoint(beatmapsetId, beatmapChecksum, beatmapId, modeInt, mods,
                acc, maxCombo, cMax, c300, c200, c100, c50, cMiss, score)
        };
    }

    [HttpGet]
    public object ManiaPpChart(long beatmapsetId, string beatmapChecksum, long beatmapId, [FromQuery] string[] mods, int totalHits)
    {
        var beatmapPath = OsuApi.GetBeatmapPath(beatmapsetId, beatmapChecksum, beatmapId);
        var res         = Plugin.Shared.Osu.PerformanceCalculator.ManiaPpChart(beatmapPath, mods, totalHits);

        return new
        {
            res.ppMax,
            res.length,
            res.multiplier
        };
    }

    [HttpGet]
    public FileStreamResult GetAccRing(double acc, int modeInt)
    {
        var ring = OsuScoreDrawer.GetAccRing("", acc, modeInt, withText: false);
        return new FileStreamResult(ring.ToStream(), "image/png");
    }

    [HttpGet]
    public FileStreamResult GetModIcon(string mod, bool withText = true)
    {
        var icon = withText ? OsuModDrawer.GetModIcon(mod) : OsuModDrawer.GetModIconWithoutText(mod);
        // return @"data:image/png;base64," + icon.ToB64(100);
        return new FileStreamResult(icon.ToStream(), "image/png");
    }

    [HttpGet]
    public FileStreamResult GetCover(long beatmapsetId, long beatmapId, string? beatmapChecksum = null)
    {
        var beatmapPath = beatmapChecksum == null
            ? OsuApi.GetBeatmapPathByBeatmapId(beatmapsetId, beatmapId)
            : OsuApi.GetBeatmapPath(beatmapsetId, beatmapChecksum, beatmapId);

        if (OsuApi.TryGetBeatmapCover(beatmapPath, out var coverPath))
        {
            return new FileStreamResult(Image.Load(coverPath).ToStream(), "image/png");
        }

        throw new FileNotFoundException("Not Found");
    }

    [HttpGet]
    public async Task<FileStreamResult> GetImage(string uri)
    {
        var img = await OsuDrawerCommon.GetCacheOrDownload(new Uri(uri));
        return new FileStreamResult(img.ToStream(), "image/png");
    }

    [HttpGet]
    public async Task<string> GetUserInfo(string username, int modeInt)
    {
        return (await OsuApi.GetUserInfoByName(username, modeInt)).ToJson();
    }

    [HttpGet]
    public async Task<string> GetRecent(long userId, int modeInt, int bpRank = 1, bool fail = false)
    {
        var recentScores = await OsuApi.GetScores(userId, OsuApi.OsuScoreType.Recent, OsuApi.GetModeName(modeInt), bpRank - 1, 1, includeFails: fail);

        if (!(recentScores?.Any() ?? false))
        {
            throw new KeyNotFoundException($"最近在 osu! {OsuApi.GetModeName(modeInt)} 上未打过图");
        }

        return recentScores[0].ToJson();
    }

    [HttpGet]
    public async Task<string> GetBest(long userId, int modeInt, int bpRank = 1)
    {
        var recentScores = await OsuApi.GetScores(userId, OsuApi.OsuScoreType.Best, OsuApi.GetModeName(modeInt), bpRank - 1, 1);

        if (!(recentScores?.Any() ?? false))
        {
            throw new KeyNotFoundException("没有哦");
        }

        return recentScores[0].ToJson();
    }

    [HttpGet]
    public async Task<string> GetBeatmapInfo(long beatmapId)
    {
        var result = await OsuApi.Request($"https://osu.ppy.sh/api/v2/beatmaps/{beatmapId}").GetStringAsync();
        return result;
    }
}