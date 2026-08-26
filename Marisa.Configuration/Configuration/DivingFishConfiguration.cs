#pragma warning disable CS8618

namespace Marisa.Configuration;

public class DivingFishConfiguration
{
    private string? _devToken;
    private string? _clientId;
    private string? _clientSecret;
    private string? _redirectUri;

    public string DevToken
    {
        get => ConfigurationManager.RequireString("divingFish.devToken", _devToken);
        set => _devToken = value;
    }

    internal string? DevTokenRaw => _devToken;

    /// <summary>OAuth 应用 client_id，配置后优先走 OAuth，否则回退 DevToken</summary>
    public string? ClientId
    {
        get => _clientId;
        set => _clientId = value;
    }

    /// <summary>OAuth 应用 client_secret（只存服务端）</summary>
    public string? ClientSecret
    {
        get => _clientSecret;
        set => _clientSecret = value;
    }

    /// <summary>OAuth 授权码回调地址（公网 HTTPS，须与控制台登记完全一致）</summary>
    public string? RedirectUri
    {
        get => _redirectUri;
        set => _redirectUri = value;
    }

    internal string? ClientIdRaw => _clientId;

    internal string? ClientSecretRaw => _clientSecret;

    internal string? RedirectUriRaw => _redirectUri;
}
