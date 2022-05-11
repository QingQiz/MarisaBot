#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Configuration;

public class RandomPictureConfiguration
{
    public string ImageDatabasePath { get; set; }

    public string ImageDatabaseKanKanPath { get; set; }
    
    public Dictionary<string, string[]> Alias { get; set; }
    
    public string[] FileNameExclude { get; set; }
    
    public string[] AvailableFileExt { get; set; }
}