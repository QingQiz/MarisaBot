using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Marisa.Utils;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public class DxRating
{
    public readonly long AdditionalRating;
    public readonly List<SongScore> NewScores = new();
    public readonly List<SongScore> OldScores = new();
    public readonly string Nickname;
    public readonly bool B50;

    public DxRating(dynamic data, bool b50)
    {
        AdditionalRating = data.additional_rating;
        Nickname         = data.nickname;
        B50              = b50;
        foreach (var d in data.charts.dx) NewScores.Add(new SongScore(d));

        foreach (var d in data.charts.sd) OldScores.Add(new SongScore(d));

        if (B50)
        {
            NewScores.ForEach(s => s.Rating = s.B50Ra());
            OldScores.ForEach(s => s.Rating = s.B50Ra());
        }

        NewScores = NewScores.OrderByDescending(s => s.Rating).ToList();
        OldScores = OldScores.OrderByDescending(s => s.Rating).ToList();
    }

    #region Drawer

    private Bitmap GetScoreCard(SongScore score)
    {
        var color = MaiMaiSong.LevelColor;

        var (coverBackground, coverBackgroundAvgColor) = ResourceManager.GetCoverBackground(score.Id);

        var card = new Bitmap(210, 40);

        using (var g = Graphics.FromImage(card))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
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
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

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

    private Bitmap GetB40Card()
    {
        const int column = 5;
        var       row    = (OldScores.Count + column - 1) / column + (NewScores.Count + column - 1) / column;

        const int cardWidth  = 400;
        const int cardHeight = 200;

        const int paddingH = 30;
        const int paddingV = 30;

        const int bgWidth  = cardWidth * column + paddingH * (column + 1);
        var       bgHeight = cardHeight * row + paddingV * (row + 4);


        var background = new Bitmap(bgWidth, bgHeight);

        using (var g = Graphics.FromImage(background))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

            var pxInit = paddingH;
            var pyInit = paddingV;

            var px = pxInit;
            var py = pyInit;

            for (var i = 0; i < OldScores.Count; i++)
            {
                g.DrawImage(GetScoreCard(OldScores[i]), px, py);

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

            var sdRowCount = (OldScores.Count + column - 1) / column;
            pxInit = paddingH;
            pyInit = cardHeight * sdRowCount + paddingV * (sdRowCount + 1 + 3);

            g.FillRectangle(new SolidBrush(Color.FromArgb(120, 136, 136)),
                new Rectangle(paddingH, pyInit - 2 * paddingV, bgWidth - 2 * paddingH, paddingV / 2));

            px = pxInit;
            py = pyInit;

            for (var i = 0; i < NewScores.Count; i++)
            {
                g.DrawImage(GetScoreCard(NewScores[i]), px, py);

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
        var (dxRating, sdRating) = (NewScores.Sum(s => s.Rating), OldScores.Sum(s => s.Rating));

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
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
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
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
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
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
            using (var font = new Font("MotoyaLMaru", 30, FontStyle.Bold))
            {
                g.DrawString(
                    B50
                        ? $"旧谱 {sdRating} + 新谱 {dxRating}"
                        : $"底分 {sdRating + dxRating} + 段位 {addRating}"
                  , font, new SolidBrush(Color.Black), 140, 12);
            }
        }

        var userInfoCard = new Bitmap(nameCard.Width + 6,
            ratingCard.Height + nameCard.Height + rainbowCard.Height + 20);

        using (var g = Graphics.FromImage(userInfoCard))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
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
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
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
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.Clear(Color.FromArgb(75, 181, 181));
            g.DrawImage(ratCard, 0, 0);
            g.DrawImage(b40, 0, ratCard.Height);
        }

        return background.ToB64();
    }

    public Bitmap? GetRecommendCards(List<MaiMaiSong> songList)
    {
        SongScore?[]  score = { null, null };
        MaiMaiSong?[] song  = { null, null };

        // 计算rating
        NewScores.ForEach(s => s.Rating = s.Ra());
        OldScores.ForEach(s => s.Rating = s.Ra());

        // 找到旧谱里能推的
        if (OldScores.Any(s => s.Achievement < 100.5))
        {
            score[0] = OldScores.Where(s => s.Achievement < 100.5).ToList().RandomTake();
        }

        // 找到新谱里能推的
        if (NewScores.Any(s => s.Achievement < 100.5))
        {
            score[1] = NewScores.Where(s => s.Achievement < 100.5).ToList().RandomTake();
        }

        // 找到不在b40里但定数不超过b40里最高定数的能推的旧谱
        long minSdRating = 0;
        if (OldScores.Any())
        {
            minSdRating = OldScores.Min(s => s.Rating);

            var playableSd = OldScores.Select(s => s.Constant).Distinct().Max();

            var songListSd = songList
                .Where(s => s.Info.IsNew == false)
                .Where(s => OldScores.All(ss => ss.Id != s.Id))
                .Where(s => s.Constants.Any(c => c <= playableSd && SongScore.Ra(100.5, c) > minSdRating))
                .ToList();

            if (songListSd.Any())
            {
                song[0] = songListSd.RandomTake();
            }
        }

        // 同上，但是新谱
        long minDxRating = 0;
        if (NewScores.Any())
        {
            minDxRating = NewScores.Min(s => s.Rating);

            var playableDx = NewScores.Select(s => s.Constant).Distinct().Max();

            var songListDx = songList
                .Where(s => s.Info.IsNew)
                .Where(s => NewScores.All(ss => ss.Id != s.Id))
                .Where(s => s.Constants.Any(c => c <= playableDx && SongScore.Ra(100.5, c) > minDxRating))
                .ToList();

            if (songListDx.Any())
            {
                song[1] = songListDx.RandomTake();
            }
        }

        Bitmap?[] card = { null, null, null, null };

        // 获取推荐卡片
        card[0] = score[0] == null ? null : GetRecommendCards(score[0]!, songList);
        card[1] = song[0]  == null ? null : GetRecommendCards(song[0]!, minSdRating);
        card[2] = score[1] == null ? null : GetRecommendCards(score[1]!, songList);
        card[3] = song[1]  == null ? null : GetRecommendCards(song[1]!, minDxRating);

        const int cardHeight = 240;
        const int cardWidth  = 800;

        var cnt = card.Count(x => x != null);

        if (cnt == 0)
        {
            return null;
        }

        // 把推荐卡片拼起来
        var bg = new Bitmap(cardWidth, cnt * cardHeight);

        using var g = Graphics.FromImage(bg);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.HighQuality;

        var y = 0;

        foreach (var c in card)
        {
            if (c == null) continue;

            g.DrawImage(c, 0, y);
            y += cardHeight;
            g.DrawLine(new Pen(Color.DarkGray, 2), 0, y - 1, cardWidth, y - 1);
        }

        g.DrawLine(new Pen(Color.DarkGray, 2), 0, 0, cardWidth, 0);

        return bg;
    }

    private static Bitmap GetRecommendCards(SongScore score, List<MaiMaiSong> songList)
    {
        const int padding = 20;

        var cover = ResourceManager.GetCover(score.Id);
        var bg    = new Bitmap(800, cover.Height + padding * 2);

        var song  = songList.First(s => s.Id == score.Id);

        using var g = Graphics.FromImage(bg);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.HighQuality;
        g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
        
        using var titleFont = new Font("Consolas", 27, FontStyle.Bold);
        using var font      = new Font("Consolas", 18);
        using var fontS     = new Font("Consolas", 14, FontStyle.Italic | FontStyle.Bold);
        using var fontM     = new Font("Consolas", 14, FontStyle.Bold);
        using var fontH     = new Font("Consolas", 20, FontStyle.Bold);

        var y = (bg.Height - cover.Height) / 2;
        var x = padding + cover.Width + padding;

        g.Clear(Color.White);
        g.DrawImage(cover, padding, y);

        var nextA = score.NextRa();
        var nextR = SongScore.Ra(nextA, score.Constant);

        var lvStr   = $"{MaiMaiSong.LevelName[(int)score.LevelIdx]}: {score.Constant}";
        var measure = g.MeasureString(lvStr, fontS);
        g.DrawString(lvStr, fontS, new SolidBrush(MaiMaiSong.LevelColor[score.LevelIdx]),
            bg.Width - padding - measure.Width, bg.Height - padding - measure.Height);

        g.DrawStrings(new List<(string, Font, Brush)>
        {
            (song.Title, titleFont, Brushes.Black),
            ($"\n当前达成率为: {score.Achievement:F4}\n", font, Brushes.Black),
            ($"    Rating为: {score.Rating}\n", font, Brushes.Black),
            ($"推分到达成率: {nextA:F4}", font, Brushes.Black),
            ($"(+{nextA - score.Achievement:F4})\n", font, Brushes.SpringGreen),
            ($"    Rating为: {nextR}", font, Brushes.Black),
            ($"(+{nextR - score.Rating})\n", font, Brushes.SpringGreen),
            ("可推", fontH, Brushes.Black),
            ($"{nextR - score.Rating}", fontH, Brushes.Red),
            ("分", fontH, Brushes.Black),
            ($"({(song.Info.IsNew ? "新谱" : "旧谱")})", fontM, Brushes.Gray),
        }, x, y);

        return bg;
    }

    private static Bitmap GetRecommendCards(MaiMaiSong song, long minRating)
    {
        const int padding = 20;

        var cover = ResourceManager.GetCover(song.Id);
        var bg    = new Bitmap(800, cover.Height + padding * 2);

        using var g = Graphics.FromImage(bg);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.HighQuality;

        using var titleFont = new Font("Consolas", 27, FontStyle.Bold);
        using var font      = new Font("Consolas", 18);
        using var fontS     = new Font("Consolas", 14, FontStyle.Italic | FontStyle.Bold);
        using var fontM     = new Font("Consolas", 14, FontStyle.Bold);
        using var fontH     = new Font("Consolas", 20, FontStyle.Bold);

        var y = (bg.Height - cover.Height) / 2;
        var x = padding + cover.Width + padding;

        g.Clear(Color.White);
        g.DrawImage(cover, padding, y);

        var constantIndex = song.Constants.FindIndex(c => SongScore.NextRa(c, minRating) > 0);
        var constant      = song.Constants[constantIndex];

        var nextA = SongScore.NextRa(constant, minRating);
        var nextR = SongScore.Ra(nextA, constant);

        var lvStr   = $"{MaiMaiSong.LevelName[constantIndex]}: {constant}";
        var measure = g.MeasureString(lvStr, fontS);

        g.DrawString(lvStr, fontS, new SolidBrush(MaiMaiSong.LevelColor[constantIndex]),
            bg.Width - padding - measure.Width, bg.Height - padding - measure.Height);

        g.DrawStrings(new List<(string, Font, Brush)>
        {
            (song.Title, titleFont, Brushes.Black),
            ($"\n歌曲不在你的B40里,地板: {minRating}\n ", font, Brushes.Peru),
            ($"推分到达成率: {nextA:F4}\n", font, Brushes.Black),
            ($"    Rating为: {nextR}", font, Brushes.Black),
            ($"(+{nextR - minRating})\n", font, Brushes.SpringGreen),
            ("可推", fontH, Brushes.Black),
            ($"{nextR - minRating}", fontH, Brushes.Red),
            ("分", fontH, Brushes.Black),
            ($"({(song.Info.IsNew ? "新谱" : "旧谱")})", fontM, Brushes.Gray),
        }, x, y, 4);

        return bg;
    }

    #endregion
}