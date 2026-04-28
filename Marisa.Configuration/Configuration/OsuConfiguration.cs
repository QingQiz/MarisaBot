#pragma warning disable CS8618

namespace Marisa.Configuration;

public class OsuConfiguration
{
    private string? _clientId;
    private string? _clientSecret;

    public string ClientId
    {
        get => ConfigurationManager.RequireString("osu.clientId", _clientId);
        set => _clientId = value;
    }

    internal string? ClientIdRaw => _clientId;

    public string ClientSecret
    {
        get => ConfigurationManager.RequireString("osu.clientSecret", _clientSecret);
        set => _clientSecret = value;
    }

    internal string? ClientSecretRaw => _clientSecret;

    public string TempPath { get; set; }
    public string ResourcePath { get; set; }
}
