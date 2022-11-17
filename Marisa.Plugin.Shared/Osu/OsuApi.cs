using System.Net;
using System.Web;
using Flurl;
using Flurl.Http;
using log4net;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;
using NUnit.Framework;

namespace Marisa.Plugin.Shared.Osu;

public static class OsuApi
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
        catch (FlurlHttpException e)
        {
            if (retry != 0) return await GetUserInfoByName(username, mode, retry - 1);

            LogManager.GetLogger(nameof(OsuApi)).Error(e.ToString());
            throw new Exception($"Network Error While Getting User: {e.Message}");
        }
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
        catch (FlurlHttpException e)
        {
            if (retry != 0) return await GetScores(osuId, type, gameMode, skip, take, includeFails, retry - 1);

            LogManager.GetLogger(nameof(OsuApi)).Error(e.ToString());
            throw new Exception($"Network Error While Retrieving Scores: {e.Message}");
        }
    }

    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression                    = DecompressionMethods.All,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });

    public static async Task<string> DownloadBeatmap(long beatmapId, string path)
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
        request.Headers.TryAddWithoutValidation("user-agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36");

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

    // https://github.com/Monodesu/KanonBot/blob/f0f6bb1edcc4e460da6550a5626c5796441bf007/src/functions/osu/get.cs#L119
    public static (double scorePp, double bonusPp, long rankedScores) BonusPp(OsuUserInfo info, OsuScore[] scores)
    {
        double scorePp = 0.0, bonusPp = 0.0, pp = 0.0, sumOxy = 0.0, sumOx2 = 0.0, avgX = 0.0, avgY = 0.0, sumX = 0.0;

        List<double> ys = new();

        for (var i = 0; i < scores.Length; ++i)
        {
            var tmp = scores[i].Pp * Math.Pow(0.95, i) ?? 0;
            scorePp += tmp;
            ys.Add(Math.Log10(tmp) / Math.Log10(100));
        }

        // calculateLinearRegression
        for (var i = 1; i <= ys.Count; ++i)
        {
            var weight = MathExt.Log1P(i + 1.0);
            sumX += weight;
            avgX += i * weight;
            avgY += ys[i - 1] * weight;
        }

        avgX /= sumX;
        avgY /= sumX;
        for (var i = 1; i <= ys.Count; ++i)
        {
            sumOxy += (i - avgX) * (ys[i - 1] - avgY) * MathExt.Log1P(i + 1.0);
            sumOx2 += Math.Pow(i - avgX, 2.0) * MathExt.Log1P(i + 1.0);
        }

        var oxy = sumOxy / sumX;
        var ox2 = sumOx2 / sumX;
        // end
        var b = new double[] { avgY - (oxy / ox2) * avgX, oxy / ox2 };
        for (double i = 100; i <= info.Statistics.PlayCount; ++i)
        {
            var val = Math.Pow(100.0, b[0] + b[1] * i);
            if (val <= 0.0)
            {
                break;
            }

            pp += val;
        }

        scorePp += pp;
        bonusPp =  info.Statistics.Pp - scorePp;
        var totalScores = info.Statistics.GradeCounts["a"] + info.Statistics.GradeCounts["s"] + info.Statistics.GradeCounts["sh"] +
            info.Statistics.GradeCounts["ss"] + info.Statistics.GradeCounts["ssh"];
        bool max;
        if (totalScores >= 25397 || bonusPp >= 416.6667)
            max = true;
        else
            max = false;
        var rankedScores = max ? Math.Max(totalScores, 25397) : (int)Math.Round(Math.Log10(-(bonusPp / 416.6667) + 1.0) / Math.Log10(0.9994));

        if (!double.IsNaN(scorePp) && !double.IsNaN(bonusPp)) return (scorePp, bonusPp, rankedScores);

        return (0, 0, 0);
    }

    public enum OsuScoreType
    {
        Recent,
        Best
    }
}