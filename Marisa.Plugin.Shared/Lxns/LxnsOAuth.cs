using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Flurl.Http;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.Lxns;

public static class LxnsOAuth
{
    private const string OAuthBaseUrl = "https://maimai.lxns.net/api/v0/oauth";
    private const string AuthPageUrl = "https://maimai.lxns.net/oauth/authorize";

    private static string ClientId => ConfigurationManager.Configuration.Lxns.Oauth.ClientId;

    // ── PKCE ──

    public static (string Verifier, string Challenge) GeneratePkcePair()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var verifier = Base64UrlEncode(bytes);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // ── 授权 URL ──

    public static string GetAuthorizationUrl(string codeChallenge, string state)
    {
        // 无回调模式: 填写应用信息中的回调地址, redirect_uri 必填但授权后不跳转而是显示码
        return $"{AuthPageUrl}?client_id={ClientId}&response_type=code&redirect_uri=urn:ietf:wg:oauth:2.0:oob&code_challenge={codeChallenge}&code_challenge_method=S256&state={HttpUtility.UrlEncode(state)}&scope=read_player";
    }

    // ── Token 交换 ──

    public static async Task<LxnsToken> ExchangeCode(string code, string codeVerifier)
    {
        var response = await $"{OAuthBaseUrl}/token"
            .AllowHttpStatus("400,401")
            .PostJsonAsync(new
            {
                grant_type = "authorization_code",
                client_id = ClientId,
                redirect_uri = "urn:ietf:wg:oauth:2.0:oob",
                code,
                code_verifier = codeVerifier
            });

        var json = await response.GetStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 错误响应 (新 OAuth 标准格式)
        if (root.TryGetProperty("error", out var err))
        {
            var desc = root.TryGetProperty("error_description", out var ed)
                ? ed.GetString() : err.GetString();
            throw new HttpRequestException($"[Lxns OAuth] {err.GetString()}: {desc}");
        }

        // 优先读取顶层 fields (OAuth 2.0 标准), 回退 data 包装 (旧格式, 即将废弃)
        if (!root.TryGetProperty("access_token", out _) && root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
            root = d;

        return new LxnsToken
        {
            AccessToken = root.GetProperty("access_token").GetString()!,
            RefreshToken = root.GetProperty("refresh_token").GetString()!,
            ExpiresAt = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32())
        };
    }

    // ── Token 刷新 ──

    public static async Task<LxnsToken> RefreshToken(string refreshToken)
    {
        var response = await $"{OAuthBaseUrl}/token"
            .AllowHttpStatus("400,401")
            .PostJsonAsync(new
            {
                grant_type = "refresh_token",
                client_id = ClientId,
                refresh_token = refreshToken
            });

        var json = await response.GetStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 错误响应 (新 OAuth 标准格式)
        if (root.TryGetProperty("error", out var err))
        {
            var desc = root.TryGetProperty("error_description", out var ed)
                ? ed.GetString() : err.GetString();
            throw new HttpRequestException($"[Lxns OAuth] {err.GetString()}: {desc}");
        }

        // 优先读取顶层 fields, 回退 data 包装
        if (!root.TryGetProperty("access_token", out _) && root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
            root = d;

        return new LxnsToken
        {
            AccessToken = root.GetProperty("access_token").GetString()!,
            RefreshToken = root.TryGetProperty("refresh_token", out var rt)
                ? rt.GetString()!
                : refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32())
        };
    }
}

public class LxnsToken
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
