#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace Marisa.Configuration;

public class MaiMaiSecret
{
    private string? _maiSalt;
    private string? _aimeSalt;
    private string? _aesKey;
    private string? _aesIv;

    public string MaiSalt
    {
        get => ConfigurationManager.RequireString("maimai.secret.maiSalt", _maiSalt);
        set => _maiSalt = value;
    }

    internal string? MaiSaltRaw => _maiSalt;

    public string AimeSalt
    {
        get => ConfigurationManager.RequireString("maimai.secret.aimeSalt", _aimeSalt);
        set => _aimeSalt = value;
    }

    internal string? AimeSaltRaw => _aimeSalt;

    public string AesKey
    {
        get => ConfigurationManager.RequireString("maimai.secret.aesKey", _aesKey);
        set => _aesKey = value;
    }

    internal string? AesKeyRaw => _aesKey;

    public string AesIv
    {
        get => ConfigurationManager.RequireString("maimai.secret.aesIv", _aesIv);
        set => _aesIv = value;
    }

    internal string? AesIvRaw => _aesIv;
}
