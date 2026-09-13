using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.DivingFish;

public static class DivingFishOAuth
{
    private const string AuthBaseUrl = "https://auth.diving-fish.com";
    private const string DiscoveryUrl = AuthBaseUrl + "/.well-known/openid-configuration";
    private const string OnBehalfOfGrantType = "urn:diving-fish:params:oauth:grant-type:on-behalf-of";

    public const string CallbackPath = "/oauth/callback/divingfish";

    private static readonly Uri AuthBaseUri = new(AuthBaseUrl);
    private static readonly SemaphoreSlim DiscoveryGate = new(1, 1);
    private static DiscoveryCache? _discoveryCache;

    private static string ClientId => ConfigurationManager.Configuration.DivingFish.ClientId ?? "";
    private static string ClientSecret => ConfigurationManager.Configuration.DivingFish.ClientSecret ?? "";
    private static string RedirectUri => ConfigurationManager.Configuration.DivingFish.RedirectUri ?? "";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public static bool CanAuthorize => IsConfigured && IsAllowedRedirectUri(RedirectUri);

    public static string ScopeOf(string game)
    {
        if (string.Equals(game, "maimai", StringComparison.OrdinalIgnoreCase)) return "prober.records.read";
        if (string.Equals(game, "chunithm", StringComparison.OrdinalIgnoreCase)) return "chunithm.records.read";
        throw new ArgumentOutOfRangeException(nameof(game), game, "仅支持 maimai 或 chunithm");
    }

    public static string AuthorizationScopeOf(string game)
    {
        return $"openid profile {ScopeOf(game)}";
    }

    public static async Task<string> BuildAuthorizeUrl(string state, string codeChallenge, string game)
    {
        if (!CanAuthorize)
        {
            throw new InvalidOperationException("[DivingFish OAuth] 授权回调地址或客户端凭据未正确配置");
        }

        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(codeChallenge))
        {
            throw new ArgumentException("OAuth state 与 PKCE challenge 不能为空");
        }

        var endpoints = await GetEndpoints();
        var query = $"response_type=code&client_id={Uri.EscapeDataString(ClientId)}" +
                    $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                    $"&scope={Uri.EscapeDataString(AuthorizationScopeOf(game))}" +
                    $"&state={Uri.EscapeDataString(state)}" +
                    $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                    "&code_challenge_method=S256";
        return $"{endpoints.AuthorizationEndpoint}?{query}";
    }

    public static async Task<(DivingFishToken Token, string Sub, string Username)> ExchangeAuthCode(
        string code,
        string verifier,
        string game)
    {
        if (!CanAuthorize)
        {
            throw new InvalidOperationException("[DivingFish OAuth] 授权回调地址或客户端凭据未正确配置");
        }

        var endpoints = await GetEndpoints();
        using var response = await endpoints.TokenEndpoint
            .AllowAnyHttpStatus()
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["code_verifier"] = verifier
            });

        var body = await response.GetStringAsync();
        if (response.StatusCode != 200)
        {
            throw OAuthFailure("换码", response.StatusCode, body);
        }

        var token = ParseTokenResponse(body, AuthorizationScopeOf(game), "换码");
        var (sub, username) = await FetchUserInfo(token.AccessToken, endpoints.UserinfoEndpoint);
        return (token, sub, username);
    }

    public static async Task<(string Sub, string Username)> FetchUserInfo(string accessToken)
    {
        var endpoints = await GetEndpoints();
        return await FetchUserInfo(accessToken, endpoints.UserinfoEndpoint);
    }

    private static async Task<(string Sub, string Username)> FetchUserInfo(
        string accessToken,
        string userinfoEndpoint)
    {
        using var response = await userinfoEndpoint
            .WithOAuthBearerToken(accessToken)
            .AllowAnyHttpStatus()
            .GetAsync();
        var body = await response.GetStringAsync();

        if (response.StatusCode != 200)
        {
            throw OAuthFailure("userinfo", response.StatusCode, body);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw OAuthProtocolFailure("userinfo", "响应不是 JSON object");
            }

            var sub = ReadString(root, "sub");
            if (string.IsNullOrWhiteSpace(sub))
            {
                throw OAuthProtocolFailure("userinfo", "缺少 sub");
            }

            try
            {
                _ = SubjectForSub(sub);
            }
            catch (ArgumentException)
            {
                throw OAuthProtocolFailure("userinfo", "sub 格式无效");
            }

            var username = ReadString(root, "preferred_username")
                           ?? ReadString(root, "nickname")
                           ?? ReadString(root, "name")
                           ?? "";
            return (sub, username);
        }
        catch (JsonException)
        {
            throw OAuthProtocolFailure("userinfo", "响应不是有效 JSON");
        }
    }

    public static async Task<DivingFishToken> FetchToken(string subject, string game)
    {
        EnsureClientCredentials();
        ValidateOnBehalfOfSubject(subject);

        var endpoints = await GetEndpoints();
        using var response = await endpoints.TokenEndpoint
            .AllowAnyHttpStatus()
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["grant_type"] = OnBehalfOfGrantType,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["subject"] = subject,
                ["scope"] = ScopeOf(game)
            });

        var body = await response.GetStringAsync();
        var error = TryReadSafeErrorCode(body);

        if (response.StatusCode == 400 && error == "consent_required")
        {
            throw new DivingFishNotBoundException();
        }

        if (response.StatusCode == 401)
        {
            throw new HttpRequestException("[DivingFish OAuth] 客户端凭据无效或应用已停用，请联系管理员");
        }

        if (response.StatusCode == 429)
        {
            throw new HttpRequestException("[DivingFish OAuth] 换票过于频繁，请稍后再试");
        }

        if (response.StatusCode != 200)
        {
            throw OAuthFailure("换票", response.StatusCode, body);
        }

        return ParseTokenResponse(body, ScopeOf(game), "换票");
    }

    public static string SubjectForQq(long qq)
    {
        if (qq <= 0) throw new ArgumentOutOfRangeException(nameof(qq), qq, "QQ 必须为正整数");
        return "ref:" + SubjectRef(qq.ToString(CultureInfo.InvariantCulture));
    }

    public static string SubjectForSub(string sub)
    {
        if (!IsValidSub(sub)) throw new ArgumentException("水鱼 sub 格式无效", nameof(sub));
        return "sub:" + sub;
    }

    public static string SubjectRef(string externalId)
    {
        EnsureClientCredentials();
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new ArgumentException("外部用户标识不能为空", nameof(externalId));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{ClientId}:{externalId}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsAllowedSubject(string? subject)
    {
        if (string.IsNullOrEmpty(subject)) return false;

        if (subject.StartsWith("ref:", StringComparison.Ordinal))
        {
            var digest = subject.AsSpan(4);
            return digest.Length == 64 && digest.ToString().All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        }

        return subject.StartsWith("sub:", StringComparison.Ordinal) && IsValidSub(subject[4..]);
    }

    private static async Task<OAuthEndpoints> GetEndpoints()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Volatile.Read(ref _discoveryCache);
        if (snapshot != null && now < snapshot.ExpiresAt) return snapshot.Endpoints;

        await DiscoveryGate.WaitAsync();
        try
        {
            now = DateTimeOffset.UtcNow;
            snapshot = Volatile.Read(ref _discoveryCache);
            if (snapshot != null && now < snapshot.ExpiresAt) return snapshot.Endpoints;

            using var response = await DiscoveryUrl.AllowAnyHttpStatus().GetAsync();
            var body = await response.GetStringAsync();
            if (response.StatusCode != 200)
            {
                throw OAuthFailure("发现文档", response.StatusCode, body);
            }

            OAuthEndpoints endpoints;
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw OAuthProtocolFailure("发现文档", "响应不是 JSON object");
                }

                endpoints = new OAuthEndpoints(
                    ReadHttpsEndpoint(root, "authorization_endpoint"),
                    ReadHttpsEndpoint(root, "token_endpoint"),
                    ReadHttpsEndpoint(root, "userinfo_endpoint"));
            }
            catch (JsonException)
            {
                throw OAuthProtocolFailure("发现文档", "响应不是有效 JSON");
            }

            var fresh = new DiscoveryCache(endpoints, now.AddHours(1));
            Volatile.Write(ref _discoveryCache, fresh);
            return endpoints;
        }
        finally
        {
            DiscoveryGate.Release();
        }
    }

    private static string ReadHttpsEndpoint(JsonElement root, string property)
    {
        var value = ReadString(root, property);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals(AuthBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != AuthBaseUri.Port ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw OAuthProtocolFailure("发现文档", $"{property} 不是安全的 HTTPS 端点");
        }

        return uri.AbsoluteUri;
    }

    private static DivingFishToken ParseTokenResponse(string body, string requiredScope, string operation)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw OAuthProtocolFailure(operation, "响应不是 JSON object");
            }

            var tokenType = ReadString(root, "token_type");
            if (!string.Equals(tokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                throw OAuthProtocolFailure(operation, "token_type 不是 Bearer");
            }

            var accessToken = ReadString(root, "access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw OAuthProtocolFailure(operation, "缺少 access_token");
            }

            var expiresIn = ReadPositiveExpiresIn(root);
            if (expiresIn == null)
            {
                throw OAuthProtocolFailure(operation, "expires_in 无效");
            }

            var scope = ReadString(root, "scope");
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw OAuthProtocolFailure(operation, "缺少 scope");
            }

            var grantedScopes = ScopeSet(scope);
            var requiredScopes = ScopeSet(requiredScope);
            if (!requiredScopes.IsSubsetOf(grantedScopes))
            {
                throw OAuthProtocolFailure(operation, "授权范围不足");
            }

            DateTime expiresAt;
            try
            {
                expiresAt = DateTime.UtcNow.AddSeconds(expiresIn.Value);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw OAuthProtocolFailure(operation, "expires_in 超出范围");
            }

            return new DivingFishToken
            {
                AccessToken = accessToken,
                Scope = string.Join(' ', grantedScopes.OrderBy(x => x, StringComparer.Ordinal)),
                ExpiresAt = expiresAt
            };
        }
        catch (JsonException)
        {
            throw OAuthProtocolFailure(operation, "响应不是有效 JSON");
        }
    }

    private static int? ReadPositiveExpiresIn(JsonElement root)
    {
        if (!root.TryGetProperty("expires_in", out var value)) return null;

        int seconds;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt32(out seconds)) return null;
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            if (!int.TryParse(value.GetString(), out seconds)) return null;
        }
        else
        {
            return null;
        }

        return seconds > 0 ? seconds : null;
    }

    private static HashSet<string> ScopeSet(string scope)
    {
        return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? ReadString(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static HttpRequestException OAuthFailure(string operation, int statusCode, string body)
    {
        var error = TryReadSafeErrorCode(body) ?? "unknown_error";
        return new HttpRequestException($"[DivingFish OAuth] {operation}失败（HTTP {statusCode}, {error}）");
    }

    private static HttpRequestException OAuthProtocolFailure(string operation, string reason)
    {
        return new HttpRequestException($"[DivingFish OAuth] {operation}响应异常：{reason}");
    }

    private static string? TryReadSafeErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var error = ReadString(document.RootElement, "error");
            if (string.IsNullOrWhiteSpace(error) || error.Length > 64) return null;
            return error.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.') ? error : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ValidateOnBehalfOfSubject(string subject)
    {
        if (!IsAllowedSubject(subject))
        {
            throw new ArgumentException("OBO subject 仅允许已确认的 sub: 或 ref: 标识", nameof(subject));
        }
    }

    private static bool IsValidSub(string? sub)
    {
        return !string.IsNullOrWhiteSpace(sub) && sub.Length <= 255 && sub.All(c => !char.IsControl(c));
    }

    private static bool IsAllowedRedirectUri(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)) return false;
        if (!uri.AbsolutePath.Equals(CallbackPath, StringComparison.Ordinal) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return true;
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
    }

    private static void EnsureClientCredentials()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("[DivingFish OAuth] 客户端凭据未配置");
        }
    }

    private sealed record OAuthEndpoints(
        string AuthorizationEndpoint,
        string TokenEndpoint,
        string UserinfoEndpoint);

    private sealed record DiscoveryCache(OAuthEndpoints Endpoints, DateTimeOffset ExpiresAt);

}

public sealed class DivingFishToken
{
    public string AccessToken { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime ExpiresAt { get; set; }

}

public sealed class DivingFishNotBoundException : Exception
{
}
