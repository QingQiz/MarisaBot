using Marisa.Plugin.Shared.Util;
using Newtonsoft.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

#pragma warning disable CS8618
public class ChunithmScore
{
    private decimal? _rating;

    [JsonProperty("ds")]
    public decimal Constant { get; set; }

    [JsonProperty("fc")]
    public string Fc { get; set; }

    /**
     * 13+,14,...
     */
    [JsonProperty("level")]
    public string Level { get; set; }

    [JsonProperty("level_index")]
    public long LevelIndex { get; set; }

    /**
     * Basic,Advanced,Expert,Master,...
     */
    [JsonProperty("level_label")]
    public string LevelLabel { get; set; }

    [JsonProperty("mid")]
    public long Id { get; set; }

    [JsonProperty("ra")]
    public decimal Rating
    {
        get => _rating ?? (decimal)(_rating = ChunithmSong.Ra(Achievement, Constant));
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    [JsonProperty("score")]
    public int Achievement { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    public string Rank => GetRank(Achievement);

    public static string GetRank(int achievement)
    {
        return achievement switch
        {
            >= 100_9000 => "sssp",
            >= 100_7500 => "sss",
            >= 100_5000 => "ssp",
            >= 100_0000 => "ss",
            >= 99_0000  => "sp",
            >= 97_5000  => "s",
            >= 95_0000  => "aaa",
            >= 92_5000  => "aa",
            >= 90_0000  => "a",
            >= 80_0000  => "bbb",
            >= 70_0000  => "bb",
            >= 60_0000  => "b",
            >= 50_0000  => "c",
            _           => "d"
        };
    }

    public Image Draw()
    {
        var exo2 = SystemFonts.Get("Exo 2");
        var mm   = SystemFonts.Get("MotoyaLMaru");

        var (coverBackground, coverDominantColor) = ResourceManager.GetCoverBackground(Id);

        const int cornerRadius  = 15;
        const int height        = 200;
        const int levelBarWidth = 7;
        const int marginX       = 10;
        const int marginY       = 10;

        var levelBar = new
        {
            PaddingLeft = cornerRadius - levelBarWidth,
            PaddingTop  = cornerRadius,
            Width       = levelBarWidth,
            Height      = height - cornerRadius * 2,
            BorderWidth = 2
        };

        var fontColor = coverDominantColor.SelectFontColor();

        // 难度指示
        coverBackground.Mutate(i => i
            .Fill(Color.White,
                new RectangleF(levelBar.PaddingLeft, levelBar.PaddingTop, levelBar.Width, levelBar.Height))
            .Fill(ChunithmSong.LevelColor[LevelLabel.ToUpper()], new RectangleF(
                levelBar.PaddingLeft + levelBar.BorderWidth, levelBar.PaddingTop + levelBar.BorderWidth,
                levelBar.Width - levelBar.BorderWidth * 2, levelBar.Height - levelBar.BorderWidth * 2))
        );

        var drawX = levelBar.PaddingLeft + levelBar.Width + marginX;
        var drawY = levelBar.PaddingTop;

        var titleFont  = mm.CreateFont(30, FontStyle.Bold);
        var constFont  = exo2.CreateFont(20);
        var ratingFont = exo2.CreateFont(20, FontStyle.Bold);
        var scoreFont  = exo2.CreateFont(60, FontStyle.Bold);

        // rating
        {
            const int ratingBorderWidth = 1;

            var rect1 = new RectangleF(0, 0, 60, 30);
            var rect2 = new RectangleF(ratingBorderWidth, ratingBorderWidth, rect1.Width - ratingBorderWidth * 2,
                rect1.Height - ratingBorderWidth * 2);

            var color1 = Color.ParseHex("#c8c8c8");
            var color2 = ChunithmSong.LevelColor[LevelLabel.ToUpper()];

            var img1 = new Image<Rgba32>((int)rect1.Width, (int)rect1.Height);
            var img2 = img1.CloneAs<Rgba32>();

            img1.Clear(color2).Mutate(i => i.Fill(color1, rect2));
            img2.Clear(color2);

            img1.DrawTextCenter(Constant.ToString("F1"), ratingFont, Color.Black);
            img2.DrawTextCenter(ChunithmSong.Ra(Achievement, Constant).ToString("F2"), ratingFont, Color.White);

            coverBackground
                .DrawImage(img1, drawX, drawY)
                .DrawImage(img2, drawX + img1.Width, drawY);

            drawY += img1.Height + marginY;
        }

        // title
        {
            var title = Title;

            var measure = title.Measure(titleFont);

            while (measure.Width > height * 2 - drawX)
            {
                title   = title[..^4] + "...";
                measure = title.Measure(titleFont);
            }

            coverBackground.DrawText(title, titleFont, fontColor, drawX, drawY);

            drawY += (int)measure.Height + marginY + 5;
        }

        // score
        {
            var score = Achievement.ToString("N0");

            var measure = score.Measure(scoreFont);

            coverBackground.DrawText(score, scoreFont, fontColor, drawX, drawY - measure.Y);

            drawY += (int)measure.Height + marginY;
        }

        // fc & rank
        {
            var h = height - drawY - cornerRadius;

            var fcImg = ResourceManager.GetImage(string.IsNullOrEmpty(Fc)
                ? "icon_blank.png"
                : $"icon_{Fc.ToLower()}.png", 190);

            var rank = ResourceManager.GetImage($"rank_{Rank.ToLower()}.png").ResizeY(fcImg.Height);

            coverBackground
                .DrawImage(fcImg, drawX, height - cornerRadius - fcImg.Height)
                .DrawImage(rank, drawX + fcImg.Width + marginX, height - cornerRadius - fcImg.Height);
        }

        return coverBackground;
    }
}