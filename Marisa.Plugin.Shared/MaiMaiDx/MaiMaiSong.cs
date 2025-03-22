using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.Cacheable;
using Marisa.Plugin.Shared.Util.SongDb;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public class MaiMaiSong : Song
{
    public static readonly Color[] LevelColor =
    [
        Color.FromRgb(82, 231, 43),
        Color.FromRgb(255, 168, 1),
        Color.FromRgb(255, 90, 102),
        Color.FromRgb(198, 79, 228),
        Color.FromRgb(219, 170, 255)
    ];

    public static readonly string[] Plates =
    [
        "maimai",
        "maimai PLUS",
        "maimai GreeN",
        "maimai GreeN PLUS",
        "maimai ORANGE",
        "maimai ORANGE PLUS",
        "maimai PiNK",
        "maimai PiNK PLUS",
        "maimai MURASAKi",
        "maimai MURASAKi PLUS",
        "maimai MiLK",
        "MiLK PLUS",
        "maimai FiNALE",
        "maimai でらっくす",
        "maimai でらっくす Splash",
        "maimai でらっくす UNiVERSE",
        "maimai でらっくす FESTiVAL",
        "maimai でらっくす BUDDiES"
    ];

    public static readonly List<string> LevelNameAll =
    [
        "Basic",
        "Advanced",
        "Expert",
        "Master",
        "Re:Master"
    ];

    public static readonly List<string> LevelNameZh =
    [
        "绿",
        "黄",
        "红",
        "紫",
        "白"
    ];
    public readonly List<MaiMaiSongChart> Charts = new();
    public readonly MaiMaiSongInfo Info;
    public readonly string Type;

    public MaiMaiSong(dynamic data)
    {
        Id      = long.Parse(data.id);
        Title   = data.title;
        Title   = Title.Trim();
        Type    = data.type;
        Info    = new MaiMaiSongInfo(data.basic_info);
        Artist  = Info.Artist;
        Bpm     = Info.Bpm;
        Version = Info.From;

        // 宴定数归零
        foreach (var c in data.ds) Constants.Add(Id > 100000 ? 0 : c);

        foreach (var l in data.level)
        {
            Levels.Add(l);
        }

        for (var i = 0; i < Levels.Count; i++)
        {
            DiffNames.Add(LevelNameAll[i]);
        }

        foreach (var c in data.charts)
        {
            Charts.Add(new MaiMaiSongChart(c));

            var charter = c.charter == "-" ? "N/A" : c.charter;
            Charters.Add(charter);
        }
    }

    public bool NoCover =>
        !File.Exists($"{ResourceManager.ResourcePath}/cover/{Id}.jpg") &&
        !File.Exists($"{ResourceManager.ResourcePath}/cover/{Id}.png");

    public int Ra(int idx, double achievement)
    {
        return SongScore.Ra(achievement, Constants[idx]);
    }

    public override string MaxLevel()
    {
        return Levels.Last();
    }

    public (double TapScore, double BonusScore) NoteScore(int levelIdx)
    {
        var notes = Charts[levelIdx].Notes;

        var tap    = notes[0];
        var hold   = notes[1];
        var slide  = notes[2];
        var touch  = Type == "DX" ? notes[3] : 0;
        var @break = notes.Last();

        var x = 100.0 / (tap + 2 * hold + 3 * slide + 5 * @break + touch);
        var y = 1.0 / @break;
        return (x, y);
    }

    public override Image GetCover()
    {
        return ResourceManager.GetCover(Id, false);
    }

    public override string GetImage()
    {
        var path = Path.Join(ResourceManager.TempPath, $"Detail.{Id}.{Hash()}.b64");
        return new CacheableText(path, () => this.Draw().ToB64()).Value;
    }
}