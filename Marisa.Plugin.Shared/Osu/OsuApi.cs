using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web;
using Flurl;
using Flurl.Http;
using log4net;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu.Drawer;
using Marisa.Plugin.Shared.Osu.Entity.AlphaOsu;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;

namespace Marisa.Plugin.Shared.Osu;

public static partial class OsuApi
{
    private static string? _token;
    private static DateTime? _tokenExpire;

    private static string Token
    {
        get
        {
            if (_tokenExpire == null || DateTime.Now > _tokenExpire || _token == null)
            {
                RenewToken().Wait();
            }

            return _token!;
        }
    }

    private const string TokenUri = "https://osu.ppy.sh/oauth/token";
    private const string UserInfoUri = "https://osu.ppy.sh/api/v2/users";
    private const string FakeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";

    public static readonly List<string> ModeList = new()
    {
        "osu", "taiko", "fruit", "mania"
    };

    /// <summary>
    /// 更新 token
    /// </summary>
    private static async Task RenewToken()
    {
        var clientId     = ConfigurationManager.Configuration.Osu.ClientId;
        var clientSecret = ConfigurationManager.Configuration.Osu.ClientSecret;


        var response = await TokenUri.PostJsonAsync(new
        {
            grant_type    = "client_credentials",
            client_id     = clientId,
            client_secret = clientSecret,
            scope         = "public"
        });

        var res = await response.GetJsonAsync();

        _token       = res.access_token;
        _tokenExpire = DateTime.Now + TimeSpan.FromSeconds(res.expires_in);
    }

    public static async Task<string> GetPPlusJsonById(long uid)
    {
        return await $"https://syrin.me/pp+/api/user/{uid}/".GetStringAsync();
    }

    public static string GetModeName(int i)
    {
        return i switch
        {
            0 => "osu",
            1 => "taiko",
            2 => "fruits",
            3 => "mania",
            _ => "mania"
        };
    }

    public static async Task<OsuUserInfo> GetUserInfoByName(string username, int mode = -1, int retry = 5)
    {
        try
        {
            var json = await $"{UserInfoUri}/{username}/{GetModeName(mode)}"
                .SetQueryParam("key", "facere")
                .WithHeader("Accept", "application/json")
                .WithOAuthBearerToken(Token)
                .GetStringAsync();
            return OsuUserInfo.FromJson(json);
        }
        catch (FlurlHttpException e) when (e.StatusCode == 404)
        {
            throw new HttpRequestException($"未知的用户 {username}");
        }
        catch (FlurlHttpException e)
        {
            if (retry != 0) return await GetUserInfoByName(username, mode, retry - 1);

            LogManager.GetLogger(nameof(OsuApi)).Error(e.ToString());
            throw new HttpRequestException($"Network Error While Getting User: {e.Message}");
        }
    }

    public static IFlurlRequest Request(string uri)
    {
        return uri.WithHeader("Accept", "application/json")
            .WithOAuthBearerToken(Token);
    }

    public static async Task<OsuScore[]?> GetScores(
        long osuId, OsuScoreType type, string gameMode, int skip, int take, bool includeFails = false, int retry = 5)
    {
        try
        {
            var t = type switch
            {
                OsuScoreType.Best   => "best",
                OsuScoreType.Recent => "recent",
                _                   => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            var json = await $"{UserInfoUri}/{osuId}/scores/{t}"
                .SetQueryParam("include_fails", includeFails ? 1 : 0)
                .SetQueryParam("mode", gameMode)
                .SetQueryParam("limit", take)
                .SetQueryParam("offset", skip)
                .WithHeader("Accept", "application/json")
                .WithOAuthBearerToken(Token)
                .GetStringAsync();

            return OsuScore.FromJson(json);
        }
        catch (FlurlHttpException e) when (e.StatusCode == 404)
        {
            throw new HttpRequestException($"未知的用户 {osuId}");
        }
        catch (FlurlHttpException e)
        {
            if (retry != 0) return await GetScores(osuId, type, gameMode, skip, take, includeFails, retry - 1);

            LogManager.GetLogger(nameof(OsuApi)).Error(e.ToString());
            throw new HttpRequestException($"Network Error While Retrieving Scores: {e.Message}");
        }
    }

    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression                    = DecompressionMethods.All,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });

    public static async Task<List<AlphaOsuRecommend>> GetRecommends(long osuUid)
    {
        await "https://alphaosu.keytoix.vip/api/v1/self/users/synchronize"
            .WithHeader("uid", osuUid)
            .WithHeader("Origin", "https://alphaosu.keytoix.vip")
            .WithHeader("User-Agent", FakeUserAgent)
            .PostJsonAsync(new object());

        var rep = await "https://alphaosu.keytoix.vip/api/v1/self/maps/recommend"
            .SetQueryParams(new
            {
                gameMode         = 3,
                keyCount         = "4,7",
                difficulty       = "0,15",
                passPercent      = "0.2,1",
                newRecordPercent = "0,1",
                search           = "",
                hidePlayed       = 0,
                rule             = 4,
                current          = 1,
                pageSize         = 100
            })
            .WithHeader("uid", osuUid)
            .WithHeader("Origin", "https://alphaosu.keytoix.vip")
            .WithHeader("User-Agent", FakeUserAgent)
            .GetStringAsync();

        var res = AlphaOsuResponse.FromJson(rep);

        if (!res.Success)
        {
            throw new HttpRequestException($"AlphaOsu Failed [{res.Code}]: {res.Message}");
        }

        return res.AlphaOsuData.Recommends;
    }

    public enum OsuScoreType
    {
        Recent,
        Best
    }

    public static bool TryGetBeatmapCover(string beatmapPath, out string? coverPath)
    {
        var lines = File.ReadLines(beatmapPath);
        var regex = BeatmapCoverMatcher();

        foreach (var line in lines)
        {
            if (regex.Match(line) is not { Success: true } match) continue;

            coverPath = Path.Join(Path.GetDirectoryName(beatmapPath), match.Groups[1].Value);
            return true;
        }

        coverPath = null;
        return false;
    }

    [GeneratedRegex(@"^\s*\d+\s*,\s*\d+\s*,\s*""(.*)""\s*,\s*\d+\s*,\s*\d+\s*$")]
    private static partial Regex BeatmapCoverMatcher();

    #region Beatmap Downloader

    private static readonly Dictionary<long, object> BeatmapDownloaderLocker = new();

    private static Func<long, string> BeatmapsetPath => beatmapsetId => Path.Join(OsuDrawerCommon.TempPath, "beatmap", beatmapsetId.ToString());

    // 从 sayobot 镜像下载 beatmap
    public static async Task<string> DownloadBeatmap(long beatmapId, string path, int retry = 10)
    {
        async Task<string> DownloadBeatmapInner()
        {
            using var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://dl.sayobot.cn/beatmaps/download/mini/{beatmapId}");
            request.Headers.TryAddWithoutValidation("authority", "dl.sayobot.cn");
            request.Headers.TryAddWithoutValidation("accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            request.Headers.TryAddWithoutValidation("accept-language", "zh-CN,zh;q=0.9,en-GB;q=0.8,en;q=0.7");
            request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Google Chrome\";v=\"105\", \"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"105\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", "document");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", "none");
            request.Headers.TryAddWithoutValidation("sec-fetch-user", "?1");
            request.Headers.TryAddWithoutValidation("upgrade-insecure-requests", "1");
            request.Headers.TryAddWithoutValidation("user-agent", FakeUserAgent);

            var response = await HttpClient.SendAsync(request);

            var filename = (HttpUtility.ParseQueryString(request.RequestUri!.Query).Get("filename") ?? beatmapId.ToString()) + ".osz";

            var beatmapPath = Path.Join(path, filename);

            var s  = await response.Content.ReadAsStreamAsync();
            var fs = File.OpenWrite(beatmapPath);

            await s.CopyToAsync(fs);

            s.Close();
            fs.Close();

            return beatmapPath;
        }

        try
        {
            return await DownloadBeatmapInner();
        }
        catch (FlurlHttpException)
        {
            if (retry == 0) throw;
            return await DownloadBeatmap(beatmapId, path, retry - 1);
        }
    }

    // 从指定的 beatmapsetId 和 beatmap 的 md5 获取 beatmap 的路径
    private static string GetBeatmapPathByMd5(long beatmapsetId, string checksum)
    {
        var path = BeatmapsetPath(beatmapsetId);

        if (Directory.Exists(path))
        {
            foreach (var f in Directory.GetFiles(path, "*.osu", SearchOption.AllDirectories))
            {
                var hash = File.ReadAllText(f).GetMd5Hash();

                if (hash.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                {
                    return f;
                }
            }
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            goto Exception;
        }

        // 如果是 windows 的话，检查是否已经安装过 osu
        var reg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\osu\DefaultIcon");

        var osuPath = reg?.GetValue(string.Empty) as string;

        // 没安装 osu 直接跳过
        if (string.IsNullOrEmpty(osuPath))
        {
            goto Exception;
        }

        osuPath = Path.GetDirectoryName(osuPath.Split(",")[0].Trim('"'));
        // osu 中已上传的图以 beatmapset id 开头，并且不会嵌套
        foreach (var p in Directory.GetDirectories(Path.Join(osuPath, "Songs"), $"{beatmapsetId}*", SearchOption.TopDirectoryOnly))
        {
            foreach (var f in Directory.GetFiles(p, "*.osu", SearchOption.AllDirectories))
            {
                var hash = File.ReadAllText(f).GetMd5Hash();

                if (hash.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                {
                    return f;
                }
            }
        }

        Exception:
        throw new FileNotFoundException($"Can not find beatmap with MD5 {checksum}");
    }

    // 从 beatmap 获取 beatmap 的路径
    public static string GetBeatmapPath(Beatmap beatmap, bool retry = true)
    {
        var path = BeatmapsetPath(beatmap.BeatmapsetId);

        object l;

        // 获取特定 beatmap set 的锁（没有的话创建一个）
        lock (BeatmapDownloaderLocker)
        {
            if (BeatmapDownloaderLocker.ContainsKey(beatmap.BeatmapsetId))
            {
                l = BeatmapDownloaderLocker[beatmap.BeatmapsetId];
            }
            else
            {
                l = BeatmapDownloaderLocker[beatmap.BeatmapsetId] = new object();
            }
        }

        // 套上这个锁，如果同时有两个下载，则会分别走 if 的两个分支
        lock (l)
        {
            // 已经额外下了谱面，要么直接获取，要么下载更新
            if (Directory.Exists(path))
            {
                // 如果谱面更新了，这里会抛异常
                try
                {
                    return GetBeatmapPathByMd5(beatmap.BeatmapsetId, beatmap.Checksum);
                }
                catch (FileNotFoundException)
                {
                    Directory.Delete(path, true);

                    // 重新下载一次，如果还找不到，那就不重试了
                    if (retry)
                    {
                        return GetBeatmapPath(beatmap, false);
                    }

                    throw;
                }
            }

            // 如果没有额外下载谱面，我们尝试找已经安装了的 osu，找里面有没有我们需要的谱面
            try
            {
                return GetBeatmapPathByMd5(beatmap.BeatmapsetId, beatmap.Checksum);
            }
            catch (FileNotFoundException)
            {
                // 没找到就额外下载
            }

            string download;
            try
            {
                download = DownloadBeatmap(beatmap.BeatmapsetId, Path.GetDirectoryName(path)!).Result;
            }
            catch (Exception e)
            {
                LogManager.GetLogger(nameof(PerformanceCalculator)).Error(e.ToString());
                throw new Exception($"Network Error While Downloading Beatmap: {e.Message}");
            }

            try
            {
                ZipFile.ExtractToDirectory(download, path);
            }
            catch (Exception e)
            {
                LogManager.GetLogger(nameof(PerformanceCalculator)).Error(e.ToString());
                throw new Exception($"A Error Occurred While Extracting Beatmap: {e.Message}");
            }

            File.Delete(download);

            var cover = Directory.GetFiles(path, "*.osu", SearchOption.AllDirectories)
                .AsParallel()
                .Select(f =>
                {
                    TryGetBeatmapCover(f, out var cover);
                    return Path.GetFileName(cover);
                })
                .Where(x => x != null)
                .Cast<string>()
                .ToHashSet();

            // 删除除了谱面文件（.osu）和封面 以外的所有文件，从而减小体积
            Parallel.ForEach(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories), f =>
            {
                if (f.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)) return;
                if (cover.Contains(Path.GetFileName(f))) return;

                File.Delete(f);
            });

            return GetBeatmapPathByMd5(beatmap.BeatmapsetId, beatmap.Checksum);
        }

        // 我们不需要删除字典里的锁，因为下载的谱面总数不会特别巨大
    }

    #endregion
}