using System.Net;
using System.Web;
using Flurl;
using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Microsoft.EntityFrameworkCore;

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

    public static async Task<OsuUserInfo> GetUserInfoByName(string username, int mode = -1)
    {
        var json = await $"{UserInfoUri}/{username}/{GetModeName(mode)}"
            .SetQueryParam("key", "facere")
            .WithHeader("Accept", "application/json")
            .WithOAuthBearerToken(Token)
            .GetStringAsync();

        return OsuUserInfo.FromJson(json);
    }

    public static async Task<OsuUserInfo?> GetUserInfo(long qq)
    {
        var db = new BotDbContext().OsuBinds;

        var bind = await db.FirstOrDefaultAsync(u => u.UserId == qq);

        if (bind == null)
        {
            return null;
        }

        var json = await $"{UserInfoUri}/{bind.OsuUserId}/{bind.GameMode ?? ""}"
            .SetQueryParam("key", "facere")
            .WithHeader("Accept", "application/json")
            .WithOAuthBearerToken(Token)
            .GetStringAsync();

        return OsuUserInfo.FromJson(json);
    }

    public static async Task<OsuScore[]?> GetScores(long osuId, OsuScoreType type, string gameMode, int skip, int take, bool includeFails = false)
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

    private static readonly HttpClient HttpClient = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

    public static async Task<string> DownloadBeatmap(long beatmapId, string path)
    {
        using var request    = new HttpRequestMessage(new HttpMethod("GET"), "https://dl.sayobot.cn/beatmaps/download/mini/1794551");
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

        await (await response.Content.ReadAsStreamAsync()).CopyToAsync(File.OpenWrite(beatmapPath));

        return beatmapPath;
    }
}

public enum OsuScoreType
{
    Recent,
    Best
}