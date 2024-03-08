using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Utils;
using Marisa.Utils.Cacheable;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Shared.Ongeki;

public record OngekiMusicDataRecord(
    int Id,
    string Title,
    string Artist,
    string Source,
    string Genre,
    string BossCard,
    int BossLevel,
    string Version,
    string ReleaseDate,
    string CopyRight,
    List<OngekiChartRecord?> Charts
);

public record OngekiChartRecord(
    string Creator,
    double Const,
    string Bpm,
    // NoteCount = TapCount + HoldCount + SHoldCount + FlickCount
    int NoteCount,
    int TapCount,
    int HoldCount,
    int SideCount,
    int SHoldCount,
    int FlickCount,
    int BellCount
);

public class OngekiSong : Song
{
    private string Source;
    private string Genre;
    private string BossCard;
    private int BossLevel;
    private string ReleaseDate;
    private string CopyRight;
    public List<OngekiChartRecord?> Charts;

    public OngekiSong(OngekiMusicDataRecord d)
    {
        // new elem
        Source      = d.Source;
        Genre       = d.Genre;
        BossCard    = d.BossCard;
        BossLevel   = d.BossLevel;
        ReleaseDate = d.ReleaseDate;
        CopyRight   = d.CopyRight;
        Charts      = d.Charts;

        // parent elem
        Id      = d.Id;
        Title   = d.Title;
        Artist  = d.Artist;
        Version = d.Version;
        Bpm = d.Charts
            .Where(c => c is not null)
            .SelectMany(c => c!.Bpm.Split('\t'))
            .Select(double.Parse).Max();

        Constants.AddRange(d.Charts
            .Where(c => c is not null)
            .Select(c => c!.Const));

        Levels.AddRange(d.Charts
            .Select((c, i) => (c, i))
            .Where(c => c.c is not null)
            .Select(c => LevelAlias.Values.ElementAt(c.i)));

        Charters.AddRange(d.Charts
            .Where(c => c is not null)
            .Select(c => c!.Creator));
    }

    public override string MaxLevel()
    {
        return Charts.Max(x => x?.Const ?? 0).ToString("F2");
    }

    public override Image GetCover()
    {
        return ResourceManager.GetCover(Id);
    }

    public static readonly Dictionary<string, string> LevelAlias = new()
    {
        { "绿", "BASIC" },
        { "黄", "ADVANCED" },
        { "红", "EXPERT" },
        { "紫", "MASTER" },
        { "白", "Lunatic" },
    };

    public override string GetImage()
    {
        return new CacheableText(
            Path.Join(ResourceManager.TempPath, "Detail-") + Id + ".b64",
            () => WebApi.OngekiSong((int)Id).Result
        ).Value;
    }
}