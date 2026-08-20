using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼账号 OAuth（设备码绑定 + 换票）。
///     文档：https://maimai.diving-fish.com/manual/docs/developer/oauth-quickstart
/// </summary>
public static class DivingFishOAuth
{
    private const string AuthBaseUrl = "https://auth.diving-fish.com";

    private const string OnBehalfOfGrantType = "urn:diving-fish:params:oauth:grant-type:on-behalf-of";

    private const string Scope = "prober.records.read";

    private static string ClientId => ConfigurationManager.Configuration.DivingFish.ClientId ?? "";

    private static string ClientSecret => ConfigurationManager.Configuration.DivingFish.ClientSecret ?? "";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    ///     用户标识 ref 摘要：sha256($"{clientId}:{externalId}")，externalId 为应用侧标识（如 QQ 号）
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
    ///     发起设备码绑定，返回给用户打开的授权链接（10 分钟有效）
    /// </summary>
    public static async Task<string> StartBinding(string externalId)
    {
        var response = await $"{AuthBaseUrl}/oauth/device_authorization"
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = Scope,
                ["subject_ref"] = SubjectRef(externalId),
                ["binding_label"] = MaskLabel(externalId)
            });

        var body = await response.GetStringAsync();

        var uri = TryReadField(body, "verification_uri_complete");
        if (uri == null)
        {
            throw new HttpRequestException($"[DivingFish OAuth] 绑定发起失败: {body}");
        }

        return uri;
    }

    /// <summary>
    ///     换票：凭应用凭据 + 用户标识换取 access token。
    ///     未绑定时抛 <see cref="DivingFishNotBoundException"/>。
    /// </summary>
    public static async Task<DivingFishToken> FetchToken(string subject)
    {
        var response = await $"{AuthBaseUrl}/oauth/token"
            .AllowHttpStatus("400,429")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["grant_type"] = OnBehalfOfGrantType,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["subject"] = subject,
                ["scope"] = Scope
            });

        var body = await response.GetStringAsync();

        // 400 且 consent_required → 未绑定
        if (response.StatusCode == 400 && TryReadField(body, "error") == "consent_required")
        {
            throw new DivingFishNotBoundException();
        }

        if (response.StatusCode == 429)
        {
            throw new HttpRequestException("[DivingFish OAuth] 已超出今日请求上限，请明天再试");
        }

        if (response.StatusCode != 200)
        {
            throw new HttpRequestException($"[DivingFish OAuth] 换票失败: {body}");
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
    ///     换票（按 QQ 号，ref 摘要路径）
    /// </summary>
    public static Task<DivingFishToken> FetchTokenByQq(long qq)
    {
        return FetchToken("ref:" + SubjectRef(qq.ToString()));
    }
}

public class DivingFishToken
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

/// <summary>
///     用户尚未绑定（或账号不存在），需引导用户完成设备码绑定
/// </summary>
public class DivingFishNotBoundException : Exception
{
}
