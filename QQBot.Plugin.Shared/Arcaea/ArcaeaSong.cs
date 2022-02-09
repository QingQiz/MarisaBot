using System.Drawing;
using QQBot.Plugin.Shared.Util;
using QQBot.Plugin.Shared.Util.SongDb;

namespace QQBot.Plugin.Shared.Arcaea;

public class ArcaeaSong : Song
{
    public readonly string Bpm;
    public readonly string Version;
    public readonly List<string> Level;
    public readonly List<string> Constant;
    public readonly string SongPack;
    private readonly string _coverFileName;

    public string CoverFileName => Level[^1] == "/"
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
        Level          = new List<string>();
        Constant       = new List<string>();
        SongPack       = d.song_pack;
        _coverFileName = d.cover_name;

        foreach (var l in d.level) Level.Add(l);
        foreach (var l in d.constant) Constant.Add(l);
    }

    public override string MaxLevel()
    {
        return Level.Last(l => l != "/");
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
            DrawKeyValuePair("难度", string.Join(", ", Level), x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("定数", string.Join(", ", Constant), x, y, w, h, background.Width);
        }

        return background.ToB64();
    }

    #endregion
}