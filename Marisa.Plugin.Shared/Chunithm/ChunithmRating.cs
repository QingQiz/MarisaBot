using System.Globalization;
using Marisa.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

#pragma warning disable CS8618


public partial class ChunithmRating
{
    [JsonProperty("rating", Required = Required.Always)]
    public double Rating { get; set; }

    [JsonProperty("records", Required = Required.Always)]
    public Records Records { get; set; }

    [JsonProperty("username", Required = Required.Always)]
    public string Username { get; set; }

    public static ChunithmRating FromJson(string json) => JsonConvert.DeserializeObject<ChunithmRating>(json, Converter.Settings)!;
    public string ToJson() => JsonConvert.SerializeObject(this, Converter.Settings);
}

public class Records
{
    [JsonProperty("b30", Required = Required.Always)]
    public Best[] Best { get; set; }

    [JsonProperty("r10", Required = Required.Always)]
    public Best[] R10 { get; set; }
}

public class Best
{
    [JsonProperty("cid", Required = Required.Always)]
    public long CId { get; set; }

    [JsonProperty("ds", Required = Required.Always)]
    public double Constant { get; set; }

    [JsonProperty("fc", Required = Required.Always)]
    public string Fc { get; set; }

    [JsonProperty("level", Required = Required.Always)]
    public string Level { get; set; }

    [JsonProperty("level_index", Required = Required.Always)]
    public long LevelIndex { get; set; }

    [JsonProperty("level_label", Required = Required.Always)]
    public string LevelLabel { get; set; }

    [JsonProperty("mid", Required = Required.Always)]
    public long Id { get; set; }

    [JsonProperty("ra", Required = Required.Always)]
    public double Ra { get; set; }

    [JsonProperty("score", Required = Required.Always)]
    public int Score { get; set; }

    [JsonProperty("title", Required = Required.Always)]
    public string Title { get; set; }

    public string Rank => Score switch
    {
        >= 100_9000 => "sssp",
        >= 100_7500 => "sss",
        >= 100_5000 => "ssp",
        >= 100_0000 => "ss",
        >= 97_5000  => "s",
        >= 92_5000  => "aa",
        >= 90_0000  => "a",
        >= 80_0000  => "bbb",
        >= 50_0000  => "c",
        _           => "d"
    };
}

internal static class Converter
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling        = DateParseHandling.None,
        Converters =
        {
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        },
    };
}

public partial class ChunithmRating
{
    public Image Draw()
    {
        Image GetScoreCard(Best score)
        {
            var (coverBackground, coverDominantColor) = ResourceManager.GetCoverBackground(score.Id);

            var fcImg = ResourceManager.GetImage(string.IsNullOrEmpty(score.Fc)
                ? "icon_blank.png"
                : $"icon_{score.Fc.ToLower()}.png", height: 32);

            var levelBar = new
            {
                PaddingLeft = 12,
                PaddingTop  = 17,
                Width       = 7,
                Height      = 167
            };

            var fontColor = coverDominantColor.SelectFontColor();

            // 难度指示
            const int borderWidth = 1;

            coverBackground.Mutate(i => i
                .Fill(Color.White, new RectangleF(levelBar.PaddingLeft - borderWidth, levelBar.PaddingTop - borderWidth,
                    levelBar.Width + borderWidth * 2, levelBar.Height + borderWidth * 2))
                .Fill(ChunithmSong.LevelColor[score.LevelLabel.ToUpper()],
                    new RectangleF(levelBar.PaddingLeft, levelBar.PaddingTop, levelBar.Width, levelBar.Height))
            );

            var titleFontFamily   = SystemFonts.Get("MotoyaLMaru");
            var contentFontFamily = SystemFonts.Get("Consolas");

            var titleFont  = new Font(titleFontFamily, 27, FontStyle.Bold);
            var constFont  = new Font(contentFontFamily, 17);
            var ratingFont = new Font(contentFontFamily, 24);

            var title                                               = score.Title;
            while (title.Measure(titleFont).Width > 400 - 25) title = title[..^4] + "...";

            var achievement = score.Score.ToString("000,0000", new NumberFormatInfo { NumberGroupSizes = new[] { 4 } });
            var rank        = ResourceManager.GetImage($"rank_{score.Rank.ToLower()}.png", 0.8);

            coverBackground.Mutate(i => i
                // 歌曲标题
                .DrawText(title, titleFont, fontColor, 25, 15)
                // 达成率
                .DrawText(achievement, new Font(contentFontFamily, 56), fontColor, 25, 52)
                // rank 标志
                .DrawImage(rank, 25, 110)
                // 定数
                .DrawText("BASE", constFont, fontColor, 105, 112)
                .DrawText(score.Constant.ToString("00.0"), constFont, fontColor, 105, 128)
                // Rating
                .DrawText(">", ratingFont, fontColor, 152, 115)
                .DrawText(score.Ra.ToString("00.00"), ratingFont, fontColor, 175, 115)
                // card
                .DrawImage(fcImg, 25, 153)
            );

            return coverBackground;
        }

        Image GetB30Card()
        {
            const int column = 5;
            var       row    = (Records.Best.Length + column - 1) / column + (Records.R10.Length + column - 1) / column;

            const int cardWidth  = 400;
            const int cardHeight = 200;

            const int paddingH = 30;
            const int paddingV = 30;

            const int bgWidth  = cardWidth * column + paddingH * (column + 1);
            var       bgHeight = cardHeight * row + paddingV * (row + 4);


            var background = new Image<Rgba32>(bgWidth, bgHeight);

            var pxInit = paddingH;
            var pyInit = paddingV;

            var px = pxInit;
            var py = pyInit;

            for (var i = 0; i < Records.Best.Length; i++)
            {
                background.DrawImage(GetScoreCard(Records.Best[i]), px, py);

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

            var sdRowCount = (Records.Best.Length + column - 1) / column;
            pxInit = paddingH;
            pyInit = cardHeight * sdRowCount + paddingV * (sdRowCount + 1 + 3);

            background.Mutate(i => i
                // ReSharper disable once PossibleLossOfFraction
                .Fill(Color.ParseHex("#f3ce00"), new RectangleF(paddingH, pyInit - 2 * paddingV, bgWidth - 2 * paddingH, paddingV / 2))
            );

            px = pxInit;
            py = pyInit;

            for (var i = 0; i < Records.R10.Length; i++)
            {
                background.DrawImage(GetScoreCard(Records.R10[i]), px, py);

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

            return background;
        }

        Image GetRatingCard()
        {
            var consolas = SystemFonts.Get("Consolas");
            var yaHei    = SystemFonts.Get("Microsoft Yahei");

            var (r10, b30) = (Records.R10.Sum(s => s.Ra), Records.Best.Sum(s => s.Ra));

            var r = (int)((r10 + b30) / 40 * 100) / 100.0;

            var num = r switch
            {
                >= 16    => 0,
                >= 15.25 => 1,
                >= 14.5  => 2,
                >= 13.25 => 3,
                >= 12    => 4,
                >= 10    => 5,
                >= 7     => 6,
                >= 4     => 7,
                _        => 8
            };

            num = num <= 4 ? num : 10;

            // 名字
            var nameCard = ResourceManager.GetImage($"rating-{num}-w.png", height: 200);

            var fontSize = 60;
            var font     = new Font(consolas, fontSize, FontStyle.Bold);

            while (true)
            {
                var w = Username.Measure(font);

                if (w.Width < nameCard.Width / 2.0)
                {
                    nameCard.DrawText(Username, font, Color.Black, (nameCard.Width / 2f - w.Width) / 2, (nameCard.Height - w.Height) / 2);

                    var ra  = r.ToString("00.00");
                    var raM = ra.Measure(font);

                    nameCard.DrawText(ra, font, Color.Black, (nameCard.Width + raM.Width) / 2, (nameCard.Height - w.Height) / 2);
                    break;
                }

                fontSize -= 2;
                font     =  new Font(consolas, fontSize, FontStyle.Bold);
            }

            // 称号（显示底分和段位）
            var rainbowCard = ResourceManager.GetImage("rainbow.png").ResizeX(nameCard.Width);

            font = new Font(yaHei, 30, FontStyle.Bold);

            var text = $"B30 {b30 / 30:00.00} / R10 {r10 / 10:00.00}";

            var measure = text.Measure(font);

            rainbowCard.DrawText(text, font, Color.Black, (rainbowCard.Width - measure.Width) / 2, (rainbowCard.Height - measure.Height) / 2 - 10);

            var userInfoCard = new Image<Rgba32>(nameCard.Width + 6, nameCard.Height + rainbowCard.Height + 20);

            userInfoCard
                .DrawImageHCenter(nameCard, 10)
                .DrawImageHCenter(rainbowCard, nameCard.Height + 20);

            // 添加一个生草头像
            var background = new Image<Rgba32>(2180, userInfoCard.Height + 50);
            var dlx        = ResourceManager.GetImage("logo.png");
            dlx.Resize(userInfoCard.Height, userInfoCard.Height);
            background.DrawImage(dlx, 30, 20).DrawImage(userInfoCard, 30 + dlx.Width + 20, 20);

            return background;
        }

        var ratCard = GetRatingCard();
        var b40     = GetB30Card();

        var background = new Image<Rgba32>(b40.Width, ratCard.Height + b40.Height);

        background.Mutate(i => i
            .Fill(Color.ParseHex("#f1d384"))
            .DrawImage(ratCard, 0, 0)
            .DrawImage(b40, 0, ratCard.Height)
        );
        return background;
    }
}