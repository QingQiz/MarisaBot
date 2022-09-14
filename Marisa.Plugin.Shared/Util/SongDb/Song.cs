using SixLabors.ImageSharp;

namespace Marisa.Plugin.Shared.Util.SongDb;

public abstract class Song
{
    public long Id;
    public string Title = "";
    public string Artist = "";
    public readonly List<double> Constants = new();
    public readonly List<string> Levels = new();
    public readonly List<string> Charters = new();
    public string Bpm = "";
    public string Version = "";


    public abstract string MaxLevel();
    public abstract string GetImage();

    public abstract Image GetCover();
}