using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Shared.Util.SongDb;

public abstract class Song
{
    public readonly List<string> Charters = [];
    public readonly List<double> Constants = [];
    /// <summary>
    ///     14 / 14+ / 15 / ...
    /// </summary>
    public readonly List<string> Levels = [];
    /// <summary>
    ///     BASIC, ADVANCED, EXPERT, MASTER, ULTIMA, ...
    /// or
    ///     future, past, present, ...
    /// </summary>
    public readonly List<string> DiffNames = [];
    public string Artist = "";
    public double Bpm;
    public long Id;
    public string Title = "";
    public string Version = "";


    public abstract string MaxLevel();
    public abstract string GetImage();

    public abstract Image GetCover();

    public virtual string Hash()
    {
        var serialized = JsonConvert.SerializeObject(this);
        var hashed     = MD5.HashData(Encoding.UTF8.GetBytes(serialized));
        return BitConverter.ToString(hashed).Replace("-", "");
    }
}