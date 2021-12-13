using System.Collections.Generic;
using System.Drawing;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.PluginEntity.Arcaea
{
    public class ArcaeaSong
    {
        public long Id;
        public string Title;
        public string Author;
        public string Bpm;
        public string Length;
        public List<string> Level;
        public string SongPack;
        public string CoverFileName;

        public ArcaeaSong(dynamic d)
        {
            Id            = d.id;
            Title         = d.title;
            Author        = d.author;
            Bpm           = d.bpm;
            Length        = d.length;
            Level         = new List<string>();
            SongPack      = d.song_pack;
            CoverFileName = d.cover_name;

            foreach (var l in d.level)
            {
                Level.Add(l);
            }
        }

        #region Drawer
        
        public string GetImage()
        {
            var       bgColor1 = Color.FromArgb(237, 237, 237);
            var       bgColor2 = Color.FromArgb(250, 250, 250);
            const int padding  = 0;
            const int h        = 80;

            var cover = ResourceManager.GetCover(CoverFileName).Resize(h * 6, h * 6);

            var background = new Bitmap(1300, h * 6 + padding * 2);

            using (var g = Graphics.FromImage(background))
            {
                void DrawKeyValuePair(string key, string value, int x, int y, int keyWidth, int height, int totalWidth, bool center=true)
                {
                    g.DrawImage(
                        ImageDraw.GetStringCard(key, 21, Color.Black, bgColor1, keyWidth, height, center: true),
                        x, y);
                    g.DrawImage(
                        ImageDraw.GetStringCard(value, 21, Color.Black, bgColor2, totalWidth - (x + keyWidth), height, center: center),
                        x + keyWidth, y);
                }

                g.DrawImage(cover, padding, padding);

                var x = 3 * padding + h * 6;
                var y = 0;
                var w = 200;

                DrawKeyValuePair("乐曲名", Title, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("演唱/作曲", Author, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("BPM", Bpm, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("乐曲长度", Length, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("曲包", SongPack, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("难度", string.Join(", ", Level), x, y, w, h, background.Width);
            }

            return background.ToB64();
        }

        #endregion
    }
}