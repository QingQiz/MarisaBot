using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.PluginEntity.MaiMaiDx
{
    public class MaiMaiSong
    {
        public long Id;
        public string Title;
        public string Type;
        public List<double> Constants = new();
        public List<string> Levels = new();
        public List<MaiMaiSongChart> Charts = new();
        public MaiMaiSongInfo Info;

        public MaiMaiSong(dynamic data)
        {
            Id    = long.Parse(data.id);
            Title = data.title;
            Title = Title.Trim();
            Type  = data.type;
            Info  = new MaiMaiSongInfo(data.basic_info);

            // 好像只能这样写。。。好丑。。。
            foreach (var c in data.ds)
            {
                Constants.Add(c);
            }

            foreach (var l in data.level)
            {
                Levels.Add(l);
            }

            foreach (var c in data.charts)
            {
                Charts.Add(new MaiMaiSongChart(c));
            }
        }

        #region Drawer

        public Bitmap GetSongInfoCard()
        {
            var       bgColor1 = Color.FromArgb(237, 237, 237);
            var       bgColor2 = Color.FromArgb(250, 250, 250);
            const int padding  = 10;
            const int h        = 80;

            var cover = ResourceManager.GetCover(Id);

            var background = new Bitmap(Type == "DX" ? 1200 : 1000, h * 5);

            using (var g = Graphics.FromImage(background))
            {
                g.DrawImage(cover, padding, padding);

                var x = 3 * padding + 200;
                var y = 0;
                var w = 200;

                g.DrawImage(ImageDraw.GetStringCard("乐曲名", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                g.DrawImage(ImageDraw.GetStringCard(Title, 21, Color.Black, bgColor2, background.Width - w, h), x + w,
                    y);

                y += h;
                g.DrawImage(ImageDraw.GetStringCard("演唱/作曲", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                g.DrawImage(
                    ImageDraw.GetStringCard(Info.Artist, 21, Color.Black, bgColor2, background.Width - (x + w), h),
                    x + w,
                    y);

                y += h;
                g.DrawImage(ImageDraw.GetStringCard("类别", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                g.DrawImage(
                    ImageDraw.GetStringCard(Info.Genre, 21, Color.Black, bgColor2, background.Width - (x + w), h),
                    x + w,
                    y);

                y += h;
                g.DrawImage(ImageDraw.GetStringCard("追加日期", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                g.DrawImage(
                    ImageDraw.GetStringCard(Info.ReleaseDate, 21, Color.Black, bgColor2, background.Width - (x + w), h),
                    x + w, y);

                y += h;
                g.DrawImage(ImageDraw.GetStringCard("版本", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                g.DrawImage(
                    ImageDraw.GetStringCard(Info.From, 21, Color.Black, bgColor2, background.Width - (x + w), h), x + w,
                    y);

                y = 3 * h;
                w = 100;
                g.DrawImage(ImageDraw.GetStringCard("ID", 21, Color.Black, bgColor1, w, h, center: true), 0, y);
                g.DrawImage(
                    ImageDraw.GetStringCard(Id.ToString(), 21, Color.Black, bgColor2, 3 * padding + 200 - w, h,
                        center: true), w, y);

                y += h;
                g.DrawImage(ImageDraw.GetStringCard("BPM", 21, Color.Black, bgColor1, w, h, center: true), 0, y);
                g.DrawImage(
                    ImageDraw.GetStringCard(Info.Bpm.ToString(), 21, Color.Black, bgColor2, 3 * padding + 200 - w, h),
                    w, y);
            }

            return background;
        }

        public Bitmap GetChartInfoCard()
        {
            var       bgColor1 = Color.FromArgb(237, 237, 237);
            var       bgColor2 = Color.FromArgb(250, 250, 250);
            const int h        = 80;

            var color = new[]
            {
                Color.FromArgb(82, 231, 43),
                Color.FromArgb(255, 168, 1),
                Color.FromArgb(255, 90, 102),
                Color.FromArgb(198, 79, 228),
                Color.FromArgb(219, 170, 255),
            };

            var background = new Bitmap(Type == "DX" ? 1200 : 1000, h * (Levels.Count + 1));

            using (var g = Graphics.FromImage(background))
            {
                var x = 0;
                var y = 0;
                var w = 110;

                g.DrawImage(ImageDraw.GetStringCard("难度", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                x += w;
                g.DrawImage(ImageDraw.GetStringCard("定数", 21, Color.Black, bgColor1, w, h, center: true), x, y);

                x += w;
                g.DrawImage(ImageDraw.GetStringCard("COMBO", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                x += w;
                g.DrawImage(ImageDraw.GetStringCard("TAP", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                x += w;
                g.DrawImage(ImageDraw.GetStringCard("HOLD", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                x += w;
                g.DrawImage(ImageDraw.GetStringCard("SLIDE", 21, Color.Black, bgColor1, w, h, center: true), x, y);

                if (Type == "DX")
                {
                    x += w;
                    g.DrawImage(ImageDraw.GetStringCard("TOUCH", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                }

                x += w;
                g.DrawImage(ImageDraw.GetStringCard("BREAK", 21, Color.Black, bgColor1, w, h, center: true), x, y);
                x += w;
                g.DrawImage(
                    ImageDraw.GetStringCard("谱师", 21, Color.Black, bgColor1, background.Width - x, h, center: true), x,
                    y);

                y += h;
                x =  0;
                for (var i = 0; i < Levels.Count; i++)
                {
                    g.DrawImage(ImageDraw.GetStringCard(Levels[i], 21, Color.Black, color[i], w, h, center: true), x,
                        y);
                    x += w;
                    g.DrawImage(
                        ImageDraw.GetStringCard(Constants[i].ToString("F1").Trim('0').Trim('.'), 21, Color.Black,
                            bgColor2,
                            w, h, center: true), x, y);
                    x += w;
                    g.DrawImage(
                        ImageDraw.GetStringCard(Charts[i].Notes.Sum().ToString(), 21, Color.Black, bgColor2, w, h, center: true),
                        x,
                        y);

                    foreach (var c in Charts[i].Notes)
                    {
                        x += w;
                        g.DrawImage(
                            ImageDraw.GetStringCard(c.ToString(), 21, Color.Black, bgColor2, w, h, center: true), x, y);
                    }

                    x += w;
                    g.DrawImage(
                        ImageDraw.GetStringCard(Charts[i].Charter, 21, Color.Black, bgColor2, background.Width - x, h,
                            center: true), x, y);


                    y += h;
                    x =  0;
                }
            }

            return background;
        }

        public string GetImage()
        {
            var cd1 = GetSongInfoCard();
            var cd2 = GetChartInfoCard();

            var padding = 10;

            var background = new Bitmap(cd1.Width + padding * 2, cd1.Height + cd2.Height + padding * 4);

            using (var g = Graphics.FromImage(background))
            {
                g.Clear(Color.FromArgb(250, 250, 250));
                g.DrawImage(cd1, padding, padding);
                g.DrawImage(cd2, padding, 3 * padding + cd1.Height);
            }
            
            var ms  = new MemoryStream();
            background.Save(ms, ImageFormat.Jpeg);

            return Convert.ToBase64String(ms.ToArray());
        }

        #endregion
    }
}