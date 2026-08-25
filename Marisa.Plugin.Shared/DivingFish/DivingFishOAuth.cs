using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flurl.Http;
using Flurl.Http.Configuration;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼账号 OAuth（设备码绑定 + 换票）。
///     文档：https://maimai.diving-fish.com/manual/docs/developer/oauth-quickstart
///     注意：与落雪 OAuth 不同，水鱼是"设备码绑定 + on-behalf-of 换票"，
///     且 scope 按游戏区分（maimai: prober.records.read / chunithm: chunithm.records.read）。
/// </summary>
public static class DivingFishOAuth
{
    private const string AuthBaseUrl = "https://auth.diving-fish.com";

    private const string OnBehalfOfGrantType = "urn:diving-fish:params:oauth:grant-type:on-behalf-of";

    /// <summary>水鱼域名强制直连（绕过系统代理，代理仅用于 GitHub 等）</summary>
    static DivingFishOAuth()
    {
        FlurlHttp.ConfigureClient(AuthBaseUrl, cli => cli.Settings.HttpClientFactory = new NoProxyClientFactory());
        FlurlHttp.ConfigureClient("https://www.diving-fish.com", cli => cli.Settings.HttpClientFactory = new NoProxyClientFactory());
    }

    /// <summary>禁用系统代理的 HttpClient 工厂</summary>
    private sealed class NoProxyClientFactory : DefaultHttpClientFactory
    {
        public override HttpMessageHandler CreateMessageHandler()
        {
            var handler = base.CreateMessageHandler();

            switch (handler)
            {
                case HttpClientHandler h:
                    h.UseProxy = false;
                    break;
                case SocketsHttpHandler s:
                    s.UseProxy = false;
                    break;
            }

            return handler;
        }
    }

    /// <summary>scope 按游戏区分：maimai / chunithm</summary>
    public static string ScopeOf(string game)
    {
        return game == "chunithm" ? "chunithm.records.read" : "prober.records.read";
    }

    private static string ClientId => ConfigurationManager.Configuration.DivingFish.ClientId ?? "";

    private static string ClientSecret => ConfigurationManager.Configuration.DivingFish.ClientSecret ?? "";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    ///     用户标识 ref 摘要：sha256($"{clientId}:{externalId}")，externalId 为应用侧标识（如 QQ 号）。
    ///     必须是：小写十六进制 64 位，算法/拼接与迁移时完全一致，否则存量用户映射不命中。
    /// </summary>
    public static string SubjectRef(string externalId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{ClientId}:{externalId}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    ///     遮挡展示串，用于绑定同意页的身份展示（防钓鱼）
    /// </summary>
    public static string MaskLabel(string externalId)
    {
        return externalId.Length switch
        {
            <= 4 => "QQ ****",
            _ => $"QQ {externalId[..2]}****{externalId[^2..]}"
        };
    }

    /// <summary>
    ///     发起设备码绑定，返回给用户打开的授权链接（10 分钟有效）。
    ///     scope 按游戏传：绑定什么游戏就申请对应 scope。
    /// </summary>
    public static async Task<string> StartBinding(string externalId, string game)
    {
        var response = await $"{AuthBaseUrl}/oauth/device_authorization"
            .AllowHttpStatus("400,429")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = ScopeOf(game),
                ["subject_ref"] = SubjectRef(externalId),
                ["binding_label"] = MaskLabel(externalId)
            });

        var body = await response.GetStringAsync();

        // 应用信息未补全（client profile is incomplete）等错误明确提示
        if (response.StatusCode != 200)
        {
            var err = TryReadField(body, "error") ?? response.StatusCode.ToString();
            var desc = TryReadField(body, "error_description");
            throw new HttpRequestException(
                $"[DivingFish OAuth] 绑定发起失败({err}): {desc ?? body}");
        }

        var uri = TryReadField(body, "verification_uri_complete");
        if (uri == null)
        {
            throw new HttpRequestException($"[DivingFish OAuth] 绑定发起失败: {body}");
        }

        return uri;
    }

    /// <summary>
    ///     换票：凭应用凭据 + 用户标识换取 access token（5 分钟有效，无 refresh token）。
    ///     未绑定时抛 <see cref="DivingFishNotBoundException"/>。
    ///     scope 按游戏传：查询 maimai 用 prober.records.read，chunithm 用 chunithm.records.read。
    /// </summary>
    public static async Task<DivingFishToken> FetchToken(string subject, string game)
    {
        var response = await $"{AuthBaseUrl}/oauth/token"
            .AllowHttpStatus("400,401,429")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["grant_type"] = OnBehalfOfGrantType,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["subject"] = subject,
                ["scope"] = ScopeOf(game)
            });

        var body = await response.GetStringAsync();

        // 400 consent_required → 未绑定（含 scope not granted：已授权但 scope 超范围）
        if (response.StatusCode == 400 && TryReadField(body, "error") == "consent_required")
        {
            throw new DivingFishNotBoundException();
        }

        // 401 invalid_client → 应用凭据有误
        if (response.StatusCode == 401)
        {
            throw new HttpRequestException("[DivingFish OAuth] client_id/client_secret 有误或应用已停用，请联系管理员检查配置");
        }

        // 429 slow_down → 换票过于频繁
        if (response.StatusCode == 429)
        {
            throw new HttpRequestException("[DivingFish OAuth] 换票过于频繁，请稍后再试");
        }

        if (response.StatusCode != 200)
        {
            var err = TryReadField(body, "error") ?? response.StatusCode.ToString();
            var desc = TryReadField(body, "error_description");
            throw new HttpRequestException($"[DivingFish OAuth] 换票失败({err}): {desc ?? body}");
        }

        var accessToken = TryReadField(body, "access_token");
        var expiresIn = TryReadField(body, "expires_in");

        if (accessToken == null || !int.TryParse(expiresIn, out var expiresSeconds))
        {
            throw new HttpRequestException($"[DivingFish OAuth] 换票响应异常: {body}");
        }

        return new DivingFishToken
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresSeconds)
        };
    }

    /// <summary>
    ///     获取授权账号的水鱼昵称（用于绑定二次确认）。
    ///     通过查分器 /player/records 返回的 nickname 字段（无需额外 scope）。
    /// </summary>
    public static async Task<string?> FetchNickname(string accessToken, string game)
    {
        var url = game == "chunithm"
            ? "https://www.diving-fish.com/api/chunithmprober/player/records"
            : "https://www.diving-fish.com/api/maimaidxprober/player/records";

        try
        {
            var body = await url
                .WithHeader("Authorization", $"Bearer {accessToken}")
                .AllowHttpStatus("400,401,403,429")
                .GetStringAsync();

            return TryReadField(body, "nickname") ?? TryReadField(body, "username");
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadField(string body, string field)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty(field, out var e)) return null;

            return e.ValueKind switch
            {
                JsonValueKind.String => e.GetString(),
                JsonValueKind.Number => e.GetRawText(),
                _                    => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     换票（按 QQ 号 + 游戏，ref 摘要路径）
    /// </summary>
    public static Task<DivingFishToken> FetchTokenByQq(long qq, string game)
    {
        return FetchToken("ref:" + SubjectRef(qq.ToString()), game);
    }
}

public class DivingFishToken
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

/// <summary>
///     用户尚未绑定（或账号不存在/scope 超范围），需引导用户完成设备码绑定
/// </summary>
public class DivingFishNotBoundException : Exception
{
}
