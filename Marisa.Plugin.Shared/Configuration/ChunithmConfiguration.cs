#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Configuration;

public class ChunithmConfiguration
{
    public string ResourcePath { get ; set; }
    public string TempPath { get; set; }
    public string DevToken { get; set; }

    public string RinNetKeyChip { get; set; }
    public string AllNetKeyChip { get; set; }
}