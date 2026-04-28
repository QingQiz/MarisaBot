#pragma warning disable CS8618

namespace Marisa.Configuration;

public class ChunithmConfiguration
{
    private string? _tokenLouis;
    private string? _rinNetKeyChip;
    private string? _allNetKeyChip;

    public string ResourcePath { get; set; }
    public string TempPath { get; set; }

    public string TokenLouis
    {
        get => ConfigurationManager.RequireString("chunithm.tokenLouis", _tokenLouis);
        set => _tokenLouis = value;
    }

    internal string? TokenLouisRaw => _tokenLouis;

    public string RinNetKeyChip
    {
        get => ConfigurationManager.RequireString("chunithm.rinNetKeyChip", _rinNetKeyChip);
        set => _rinNetKeyChip = value;
    }

    internal string? RinNetKeyChipRaw => _rinNetKeyChip;

    public string AllNetKeyChip
    {
        get => ConfigurationManager.RequireString("chunithm.allNetKeyChip", _allNetKeyChip);
        set => _allNetKeyChip = value;
    }

    internal string? AllNetKeyChipRaw => _allNetKeyChip;
}
