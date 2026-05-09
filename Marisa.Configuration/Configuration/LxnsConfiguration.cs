#pragma warning disable CS8618

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
}
