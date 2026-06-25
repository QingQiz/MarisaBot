#pragma warning disable CS8618

using YamlDotNet.Serialization;

namespace Marisa.Configuration;

public class LxnsConfiguration
{
    private string? _devToken;

    public string DevToken
    {
        get => ConfigurationManager.RequireString("lxns.devToken", _devToken);
        set => _devToken = value;
    }

    internal string? DevTokenRaw => _devToken;

    [YamlMember(Alias = "oauth")]
    public LxnsOauthConfiguration Oauth { get; set; } = new();
}

public class LxnsOauthConfiguration
{
    private string? _clientId;

    [YamlMember(Alias = "clientId")]
    public string ClientId
    {
        get => ConfigurationManager.RequireString("lxns.oauth.clientId", _clientId);
        set => _clientId = value;
    }
}
