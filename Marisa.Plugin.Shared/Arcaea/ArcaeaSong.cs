using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.Cacheable;
using Marisa.Plugin.Shared.Util.SongDb;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Marisa.Plugin.Shared.Arcaea;

public class ArcaeaSong : Song
{
    private readonly string _bpm;
    private readonly List<string> _diffNames = [];
    private readonly string _songPack;

    public ArcaeaSong(dynamic d)
    {
        Id            = d.id;
        Title         = d.title;
        Artist        = d.artist;
        _bpm          = d.bpm;
        Version       = d.version;
        _songPack     = d.song_pack;
        CoverFileName = d.cover_name;

        foreach (var l in d.level) Levels.Add(l);
        foreach (var l in d.constant)
        {
            if (double.TryParse((string)l, out var constant))
            {
                Constants.Add(constant);
            }
            else
            {
                Constants.Add(-1);
            }
        }
        foreach (var l in d.level_name)
        {
            _diffNames.Add(l);
        }
    }

    private string CoverFileName { get; }

    public override string MaxLevel()
    {
        return Levels.Zip(Constants).MaxBy(x => x.Second).First;
    }

    public override Image GetCover()
    {
        return ResourceManager.GetCover(CoverFileName);
    }

    #region Drawer

    public override string GetImage()
    {
        return new CacheableText(Path.Join(ResourceManager.TempPath, "Detail-") + Id + ".b64", () =>
        {
            var       bgColor1 = Color.FromRgb(237, 237, 237);
            var       bgColor2 = Color.FromRgb(250, 250, 250);
            const int padding  = 0;
            const int h        = 80;

            var cover = ResourceManager.GetCover(CoverFileName).Resize(h * 6, h * 6);

            var background = new Image<Rgba32>(1300, h * 6 + padding * 2);

            background.DrawImage(cover, padding, padding);

            const int x = 3 * padding + h * 6;
            const int w = 200;

            var y = 0;

            DrawKeyValuePair("乐曲名", Title, x, y, w, h, background.Width);
            y += h;
            DrawKeyValuePair("演唱/作曲", Artist, x, y, w, h, background.Width);
            y += h;
            DrawKeyValuePair("BPM", _bpm, x, y, w, h, background.Width);
            y += h;
            DrawKeyValuePair("曲包", _songPack, x, y, w, h, background.Width);
            y += h;
            DrawKeyValuePair("难度", string.Join(", ", _diffNames.Zip(Levels).Select(l => $"{l.First}: {l.Second}")), x, y, w, h, background.Width);
            y += h;
            DrawKeyValuePair("定数",
                string.Join(", ", Constants.Select(c => c <= 0 ? "/" : c.ToString("F1"))),
                x, y, w, h, background.Width);

            return background.ToB64();

            void DrawKeyValuePair(
                string key, string value, int x, int y, int keyWidth, int height, int totalWidth,
                bool center = true)
            {
                background.DrawImage(
                    ImageDraw.GetStringCard(key, 31, Color.Black, bgColor1, keyWidth, height, center: true),
                    x, y);
                background.DrawImage(
                    ImageDraw.GetStringCard(value, 31, Color.Black, bgColor2, totalWidth - (x + keyWidth), height,
                        center: center),
                    x + keyWidth, y);
            }
        }).Value;
    }

    #endregion
}