using Marisa.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public static class MaiMaiDrawer
{
    public static Image Draw(this MaiMaiSong song)
    {
        const int cardFontSize = 31;
        const int padding      = 10;

        Image GetSongInfoCard()
        {
            var       bgColor1 = Color.FromRgb(237, 237, 237);
            var       bgColor2 = Color.FromRgb(250, 250, 250);
            const int h        = 80;

            var cover = ResourceManager.GetCover(song.Id);

            var background = new Image<Rgba32>(song.Type == "DX" ? 1200 : 1000, h * 5);

            void DrawKeyValuePair(
                string key, string value, int x, int y, int keyWidth, int height, int totalWidth,
                bool center = false, bool overline = false)
            {
                var card1 = ImageDraw.GetStringCard(key, cardFontSize, Color.Black, bgColor1, keyWidth, height, center: true);
                var card2 = ImageDraw.GetStringCard(value, cardFontSize, Color.Black, bgColor2, totalWidth - (x + keyWidth), height, center: center);

                if (overline)
                {
                    background.Mutate(i => i
                        .DrawLines(new Pen(Color.Gray, 1), new PointF(x, y - 1), new PointF(x + totalWidth, y - 1))
                    );
                }

                background.Mutate(i => i
                    .DrawImage(card1, x, y)
                    .DrawImage(card2, x + keyWidth, y)
                );
            }

            // ReSharper disable once ConvertToConstant.Local
            var x = 3 * padding + 200;
            var y = 0;
            var w = 200;

            background.Mutate(i => i.DrawImage(cover, padding, padding));

            DrawKeyValuePair("乐曲名", song.Title, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("演唱/作曲", song.Info.Artist, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("类别", song.Info.Genre, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("追加日期", song.Info.ReleaseDate, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("版本", song.Info.From, x, y, w, h, background.Width);

            y = 3 * h;
            w = 100;
            DrawKeyValuePair("ID", song.Id.ToString(), 0, y, w, h, 3 * padding + 200, true, true);

            y += h;
            DrawKeyValuePair("BPM", song.Info.Bpm.ToString(), 0, y, w, h, 3 * padding + 200, true);

            return background;
        }

        Image GetChartInfoCard()
        {
            var bgColor1 = Color.FromRgb(237, 237, 237);
            var bgColor2 = Color.FromRgb(250, 250, 250);

            const int h = 80;
            const int w = 110;

            var background = new Image<Rgba32>(song.Type == "DX" ? 1200 : 1000, h * (song.Levels.Count + 1));

            var x = 0;
            var y = 0;

            void DrawCard(string txt, int fontSize, Color fontColor, Color bgColor, int width, int height, bool center)
            {
                background.Mutate(i => i.DrawImage(ImageDraw.GetStringCard(txt, fontSize, fontColor, bgColor, width, height, center: center), x, y));
            }

            DrawCard("难度", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("定数", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("COMBO", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("TAP", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("HOLD", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("SLIDE", cardFontSize, Color.Black, bgColor1, w, h, true);

            if (song.Type == "DX")
            {
                x += w;
                DrawCard("TOUCH", cardFontSize, Color.Black, bgColor1, w, h, true);
            }

            x += w;
            DrawCard("BREAK", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("谱师", cardFontSize, Color.Black, bgColor1, background.Width - x, h, true);

            y += h;
            x =  0;

            for (var i = 0; i < song.Levels.Count; i++)
            {
                DrawCard(song.Levels[i], cardFontSize, Color.Black, MaiMaiSong.LevelColor[i], w, h, true);
                x += w;
                DrawCard(song.Constants[i].ToString("F1").Trim('0').Trim('.'), cardFontSize, Color.Black, bgColor2, w, h, true);
                x += w;
                DrawCard(song.Charts[i].Notes.Sum().ToString(), cardFontSize, Color.Black, bgColor2, w, h, true);

                foreach (var c in song.Charts[i].Notes)
                {
                    x += w;
                    DrawCard(c.ToString(), cardFontSize, Color.Black, bgColor2, w, h, true);
                }

                x += w;
                DrawCard(song.Charters[i], cardFontSize, Color.Black, bgColor2, background.Width - x, h, true);

                y += h;
                x =  0;
            }

            return background;
        }

        var cd1 = GetSongInfoCard();
        var cd2 = GetChartInfoCard();

        var background = new Image<Rgba32>(cd1.Width + padding * 2, cd1.Height + cd2.Height + padding * 4);

        background.Mutate(i => i
            .Fill(Color.FromRgb(250, 250, 250))
            .DrawImage(cd1, padding, padding)
            .DrawImage(cd2, padding, 3 * padding + cd1.Height)
        );

        return background;
    }
}