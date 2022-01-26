using System.Drawing;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin.Shared.MaiMaiDx
{
    public class DxRating
    {
        public readonly long AdditionalRating;
        public readonly List<SongScore> DxScores = new();
        public readonly List<SongScore> SdScores = new();
        public readonly string Nickname;
        public readonly bool B50;

        public DxRating(dynamic data, bool b50)
        {
            AdditionalRating = data.additional_rating;
            Nickname         = data.nickname;
            B50              = b50;
            foreach (var d in data.charts.dx) DxScores.Add(new SongScore(d));

            foreach (var d in data.charts.sd) SdScores.Add(new SongScore(d));

            if (B50)
            {
                DxScores.ForEach(s => s.Rating = ComputeRa(s));
                SdScores.ForEach(s => s.Rating = ComputeRa(s));
            }

            DxScores = DxScores.OrderByDescending(s => s.Rating).ToList();
            SdScores = SdScores.OrderByDescending(s => s.Rating).ToList();
        }

        #region Drawer

        private Bitmap GetScoreCard(SongScore score)
        {
            var color = new[]
            {
                Color.FromArgb(82, 231, 43),
                Color.FromArgb(255, 168, 1),
                Color.FromArgb(255, 90, 102),
                Color.FromArgb(198, 79, 228),
                Color.FromArgb(219, 170, 255)
            };

            var (coverBackground, coverBackgroundAvgColor) = ResourceManager.GetCoverBackground(score.Id);

            var card = new Bitmap(210, 40);

            using (var g = Graphics.FromImage(card))
            {
                // 歌曲类别：DX 和 标准
                g.DrawImage(ResourceManager.GetImage(score.Type == "DX" ? "type_deluxe.png" : "type_standard.png"), 0,
                    0);

                // FC 标志
                var fcImg = ResourceManager.GetImage(string.IsNullOrEmpty(score.Fc)
                    ? "icon_blank.png"
                    : $"icon_{score.Fc.ToLower()}.png", 32, 32);
                g.DrawImage(fcImg, 130, 0);


                // FS 标志
                var fsImg = ResourceManager.GetImage(string.IsNullOrEmpty(score.Fs)
                    ? "icon_blank.png"
                    : $"icon_{score.Fs.ToLower()}.png", 32, 32);
                g.DrawImage(fsImg, 170, 0);
            }

            var levelBar = new
            {
                PaddingLeft = 12,
                PaddingTop  = 17,
                Width       = 7,
                Height      = 167
            };

            using (var g = Graphics.FromImage(coverBackground))
            {
                var fontColor = new SolidBrush(coverBackgroundAvgColor.SelectFontColor());

                // 难度指示
                const int borderWidth = 1;
                g.FillRectangle(new SolidBrush(Color.White), levelBar.PaddingLeft - borderWidth,
                    levelBar.PaddingTop - borderWidth,
                    levelBar.Width + borderWidth * 2, levelBar.Height + borderWidth * 2);
                g.FillRectangle(new SolidBrush(color[score.LevelIdx]), levelBar.PaddingLeft, levelBar.PaddingTop,
                    levelBar.Width, levelBar.Height);

                // 歌曲标题
                using (var font = new Font("MotoyaLMaru", 27, FontStyle.Bold))
                {
                    var title                                                   = score.Title;
                    while (g.MeasureString(title, font).Width > 400 - 25) title = title[..^4] + "...";
                    g.DrawString(title, font, fontColor, 25, 15);
                }

                var achievement = score.Achievement.ToString("F4").Split('.');

                // 达成率整数部分
                using (var font = new Font("Consolas", 36))
                {
                    g.DrawString((score.Achievement < 100 ? "0" : "") + achievement[0], font, fontColor, 20, 52);
                }

                // 达成率小数部分
                using (var font = new Font("Consolas", 27))
                {
                    g.DrawString("." + achievement[1], font, fontColor, 105, 62);
                }

                var rank = ResourceManager.GetImage($"rank_{score.Rank.ToLower()}.png");

                // rank 标志
                g.DrawImage(rank.Resize(0.8), 25, 110);

                // 定数
                using (var font = new Font("Consolas", 12))
                {
                    g.DrawString("BASE", font, fontColor, 97, 110);
                    g.DrawString(score.Constant.ToString("F1"), font, fontColor, 97, 125);
                }

                // Rating
                using (var font = new Font("Consolas", 20))
                {
                    g.DrawString(">", font, fontColor, 140, 110);
                    g.DrawString(score.Rating.ToString(), font, fontColor, 162, 110);
                }

                // card
                g.DrawImage(card, 25, 155);
            }

            return coverBackground;
        }

        /// <summary>
        /// compute b50 ra
        /// </summary>
        private static int ComputeRa(SongScore score)
        {
            var baseRa = score.Achievement switch
            {
                < 50    => 7.0,
                < 60    => 8.0,
                < 70    => 9.6,
                < 75    => 11.2,
                < 80    => 12.0,
                < 90    => 13.6,
                < 94    => 15.2,
                < 97    => 16.8,
                < 98    => 20.0,
                < 99    => 20.3,
                < 99.5  => 20.8,
                < 100   => 21.1,
                < 100.5 => 21.6,
                _       => 22.4
            };
            return (int)Math.Floor(score.Constant * (Math.Min(100.5, score.Achievement) / 100) * baseRa);
        }

        private Bitmap GetB40Card()
        {
            const int column = 5;
            var       row    = (SdScores.Count + column - 1) / column + (DxScores.Count + column - 1) / column;

            const int cardWidth  = 400;
            const int cardHeight = 200;

            const int paddingH = 30;
            const int paddingV = 30;

            const int bgWidth  = cardWidth * column + paddingH * (column + 1);
            var       bgHeight = cardHeight * row + paddingV * (row + 4);


            var background = new Bitmap(bgWidth, bgHeight);

            using (var g = Graphics.FromImage(background))
            {
                var pxInit = paddingH;
                var pyInit = paddingV;

                var px = pxInit;
                var py = pyInit;

                for (var i = 0; i < SdScores.Count; i++)
                {
                    g.DrawImage(GetScoreCard(SdScores[i]), px, py);

                    if ((i + 1) % 5 == 0)
                    {
                        px =  pxInit;
                        py += cardHeight + paddingV;
                    }
                    else
                    {
                        px += cardWidth + paddingH;
                    }
                }

                var sdRowCount = (SdScores.Count + column - 1) / column;
                pxInit = paddingH;
                pyInit = cardHeight * sdRowCount + paddingV * (sdRowCount + 1 + 3);

                g.FillRectangle(new SolidBrush(Color.FromArgb(120, 136, 136)),
                    new Rectangle(paddingH, pyInit - 2 * paddingV, bgWidth - 2 * paddingH, paddingV / 2));

                px = pxInit;
                py = pyInit;

                for (var i = 0; i < DxScores.Count; i++)
                {
                    g.DrawImage(GetScoreCard(DxScores[i]), px, py);

                    if ((i + 1) % 5 == 0)
                    {
                        px =  pxInit;
                        py += cardHeight + paddingV;
                    }
                    else
                    {
                        px += cardWidth + paddingH;
                    }
                }
            }

            return background;
        }

        private Bitmap GetRatingCard()
        {
            var (dxRating, sdRating) = (DxScores.Sum(s => s.Rating), SdScores.Sum(s => s.Rating));

            var addRating = B50 ? 0 : AdditionalRating;

            var r = dxRating + sdRating + addRating;

            var num = B50
                ? r switch
                {
                    < 2000  => r / 1000 + 1,
                    < 4000  => 3,
                    < 7000  => 4,
                    < 10000 => 5,
                    < 12000 => 6,
                    < 13000 => 7,
                    < 14500 => 8,
                    < 15000 => 9,
                    _       => 10,
                }
                : r switch
                {
                    < 8000 => r / 1000 + 1,
                    < 8500 => 9,
                    _      => 10L
                };

            // rating 部分
            var ratingCard = ResourceManager.GetImage($"rating_{num}.png");
            ratingCard = ratingCard.Resize(2);
            using (var g = Graphics.FromImage(ratingCard))
            {
                var ra = r.ToString().PadLeft(5, ' ');

                for (var i = ra.Length - 1; i >= 0; i--)
                {
                    if (ra[i] == ' ') break;
                    g.DrawImage(ResourceManager.GetImage($"num_{ra[i]}.png"), 170 + 29 * i, 20);
                }
            }

            ratingCard = ratingCard.Resize(1.4);

            // 名字
            var nameCard = new Bitmap(690, 140);
            using (var g = Graphics.FromImage(nameCard))
            {
                g.Clear(Color.White);

                var fontSize = 57;
                var font     = new Font("Consolas", fontSize, FontStyle.Bold);

                while (true)
                {
                    var w = g.MeasureString(Nickname, font);

                    if (w.Width < 480)
                    {
                        g.DrawString(Nickname, font, new SolidBrush(Color.Black), 20, (nameCard.Height - w.Height) / 2);
                        break;
                    }

                    fontSize -= 2;
                    font     =  new Font("Consolas", fontSize, FontStyle.Bold);
                }

                var dx = ResourceManager.GetImage("icon_dx.png");
                g.DrawImage(dx.Resize(3.2), 500, 10);
            }

            nameCard = nameCard.RoundCorners(20);

            // 称号（显示底分和段位）
            var rainbowCard = ResourceManager.GetImage("rainbow.png");
            rainbowCard = rainbowCard.Resize((double)nameCard.Width / rainbowCard.Width + 0.05);
            using (var g = Graphics.FromImage(rainbowCard))
            {
                using (var font = new Font("MotoyaLMaru", 30, FontStyle.Bold))
                {
                    g.DrawString(
                        B50
                            ? $"标准 {sdRating} + 地插 {dxRating}"
                            : $"底分 {sdRating + dxRating} + 段位 {addRating}"
                      , font, new SolidBrush(Color.Black), 140, 12);
                }
            }

            var userInfoCard = new Bitmap(nameCard.Width + 6,
                ratingCard.Height + nameCard.Height + rainbowCard.Height + 20);

            using (var g = Graphics.FromImage(userInfoCard))
            {
                g.DrawImage(ratingCard, 0, 0);
                g.DrawImage(nameCard, 3, ratingCard.Height    + 10);
                g.DrawImage(rainbowCard, 0, ratingCard.Height + nameCard.Height + 20);
            }

            // 添加一个生草头像
            var background = new Bitmap(2180, userInfoCard.Height + 50);
            var dlx        = ResourceManager.GetImage("dlx.png");
            dlx = dlx.Resize(userInfoCard.Height, userInfoCard.Height);
            using (var g = Graphics.FromImage(background))
            {
                g.DrawImage(dlx, 0, 20);
                g.DrawImage(userInfoCard, userInfoCard.Height + 10, 20);
            }

            return background;
        }

        public string GetImage()
        {
            var ratCard = GetRatingCard();
            var b40     = GetB40Card();

            var background = new Bitmap(b40.Width, ratCard.Height + b40.Height);

            using (var g = Graphics.FromImage(background))
            {
                g.Clear(Color.FromArgb(75, 181, 181));
                g.DrawImage(ratCard, 0, 0);
                g.DrawImage(b40, 0, ratCard.Height);
            }

            return background.ToB64();
        }

        #endregion
    }
}