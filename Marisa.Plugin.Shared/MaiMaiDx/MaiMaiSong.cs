using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Utils;
using Marisa.Utils.Cacheable;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public class MaiMaiSong : Song
{
    public readonly string Type;
    public readonly List<MaiMaiSongChart> Charts = new();
    public readonly MaiMaiSongInfo Info;

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

        // 好像只能这样写。。。好丑。。。
        foreach (var c in data.ds) Constants.Add(c);

        foreach (var l in data.level) Levels.Add(l);

        foreach (var c in data.charts)
        {
            Charts.Add(new MaiMaiSongChart(c));

            var charter = c.charter == "-" ? "N/A" : c.charter;
            Charters.Add(charter);
        }
    }

    public override string MaxLevel()
    {
        return Levels.Last();
    }

    public static readonly Color[] LevelColor =
    {
        Color.FromRgb(82, 231, 43),
        Color.FromRgb(255, 168, 1),
        Color.FromRgb(255, 90, 102),
        Color.FromRgb(198, 79, 228),
        Color.FromRgb(219, 170, 255),
    };

    public static readonly string[] Genres =
    {
        "maimai",
        "POPSアニメ",
        "ゲームバラエティ",
        "niconicoボーカロイド",
        "東方Project",
        "オンゲキCHUNITHM"
    };

    public static readonly string[] Plates =
    {
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
        "maimai でらっくす FESTiVAL"
    };

    public static readonly List<string> LevelName = new()
    {
        "Basic",
        "Advanced",
        "Expert",
        "Master",
        "Re:Master"
    };

    public static readonly List<string> LevelNameZh = new()
    {
        "绿",
        "黄",
        "红",
        "紫",
        "白"
    };

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
        var path = Path.Join(ResourceManager.TempPath, "Detail-") + Id + ".b64";

        return new CacheableText(path, () => this.Draw().ToB64()).Value;
    }
}