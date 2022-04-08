using System.Drawing;
using QQBot.Plugin.Shared.Util;
using QQBot.Plugin.Shared.Util.SongDb;

namespace QQBot.Plugin.Shared.Arcaea;

public class ArcaeaSong : Song
{
    public readonly string SongPack;
    private readonly string _coverFileName;

    private string CoverFileName => Levels[^1] == "/"
        ? _coverFileName
        : new Random().Next(2) == 0
            ? _coverFileName.Replace(".", "_byd.")
            : _coverFileName;

    public ArcaeaSong(dynamic d)
    {
        Id             = d.id;
        Title          = d.title;
        Artist         = d.artist;
        Bpm            = d.bpm;
        Version        = d.version;
        SongPack       = d.song_pack;
        _coverFileName = d.cover_name;

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
    }

    public override string MaxLevel()
    {
        return Levels.Last(l => l != "/");
    }

    public override Bitmap GetCover()
    {
        return ResourceManager.GetCover(CoverFileName);
    }

    #region Drawer

    public override string GetImage()
    {
        var       bgColor1 = Color.FromArgb(237, 237, 237);
        var       bgColor2 = Color.FromArgb(250, 250, 250);
        const int padding  = 0;
        const int h        = 80;

        var cover = ResourceManager.GetCover(CoverFileName).Resize(h * 6, h * 6);

        var background = new Bitmap(1300, h * 6 + padding * 2);

        using (var g = Graphics.FromImage(background))
        {
            void DrawKeyValuePair(string key, string value, int x, int y, int keyWidth, int height, int totalWidth,
                bool center = true)
            {
                g.DrawImage(
                    ImageDraw.GetStringCard(key, 21, Color.Black, bgColor1, keyWidth, height, center: true),
                    x, y);
                g.DrawImage(
                    ImageDraw.GetStringCard(value, 21, Color.Black, bgColor2, totalWidth - (x + keyWidth), height,
                        center: center),
                    x + keyWidth, y);
            }

            g.DrawImage(cover, padding, padding);

            var x = 3 * padding + h * 6;
            var y = 0;
            var w = 200;

            DrawKeyValuePair("乐曲名", Title, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("演唱/作曲", Artist, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("BPM", Bpm, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("曲包", SongPack, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("难度", string.Join(", ", Levels), x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("定数", 
                string.Join(", ", Constants.Select(c => c <= 0 ? "/" : c.ToString("F1"))),
                x, y, w, h, background.Width);
        }

        return background.ToB64();
    }

    #endregion
}