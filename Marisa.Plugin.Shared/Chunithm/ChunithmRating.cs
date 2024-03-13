using System.Globalization;
using System.Numerics;
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
    public decimal Rating
    {
        get
        {
            var (r10, b30) = (Records.Best.Sum(s => s.Rating), Records.R10.Sum(s => s.Rating));
            return Math.Round((r10 + b30) / 40, 2, MidpointRounding.ToZero);
        }
        // ReSharper disable once ValueParameterNotUsed
        set { }
    }

    [JsonProperty("records", Required = Required.Always)]
    public Records Records { get; set; }

    [JsonProperty("username", Required = Required.Always)]
    public string Username { get; set; }

    public decimal B30 => Math.Round(Records.Best.Sum(s => s.Rating) / 30, 2, MidpointRounding.ToZero);
    public decimal R10 => Math.Round(Records.R10.Sum(s => s.Rating) / 10, 2, MidpointRounding.ToZero);

    public static ChunithmRating FromJson(string json) => JsonConvert.DeserializeObject<ChunithmRating>(json, Converter.Settings)!;
    public string ToJson() => JsonConvert.SerializeObject(this, Converter.Settings);
}

public class Records
{
    private ChunithmScore[]? _best;

    [JsonProperty("b30")]
    public ChunithmScore[] B30
    {
        get => _best ?? Array.Empty<ChunithmScore>();
        set => _best = value;
    }

    [JsonProperty("best")]
    public ChunithmScore[] Best
    {
        get => _best ?? B30;
        set => _best = value;
    }

    [JsonProperty("r10", Required = Required.Always)]
    public ChunithmScore[] R10 { get; set; }
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

            // b30
            {
                var b30 = Records.Best.AsParallel().Select(x => x.Draw()).ToList();

                for (var i = 0; i < Records.Best.Length; i++)
                {
                    background.DrawImage(b30[i], px, py);

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

            // r10
            {
                var sdRowCount = (Records.Best.Length + column - 1) / column;
                pxInit = paddingH;
                pyInit = cardHeight * sdRowCount + paddingV * (sdRowCount + 1 + 3);

                background.Mutate(i => i
                    // ReSharper disable once PossibleLossOfFraction
                    .Fill(Color.ParseHex("#f3ce00"), new RectangleF(paddingH, pyInit - 2 * paddingV, bgWidth - 2 * paddingH, paddingV / 2))
                );

                px = pxInit;
                py = pyInit;

                var r10 = Records.R10.AsParallel().Select(x => x.Draw()).ToList();

                for (var i = 0; i < Records.R10.Length; i++)
                {
                    background.DrawImage(r10[i], px, py);

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

        Image GetRatingCard()
        {
            var consolas = SystemFonts.Get("Consolas");
            var yaHei    = SystemFonts.Get("Microsoft Yahei");

            var r = Rating;

            var num = r switch
            {
                >= 16     => 0,
                >= 15.25m => 1,
                >= 14.5m  => 2,
                >= 13.25m => 3,
                >= 12     => 4,
                >= 10     => 5,
                >= 7      => 6,
                >= 4      => 7,
                _         => 8
            };

            num = num <= 4 ? num : 10;

            // 名字
            var nameCard = ResourceManager.GetImage($"rating-{num}-w.png", height: 200);

            var fontSize = 60;
            var nameFont = new Font(consolas, 80, FontStyle.Bold);
            var rateFont = new Font(consolas, 50, FontStyle.Bold);

            var opt = ImageDraw.GetTextOptions(nameFont);

            while (true)
            {
                var w = Username.Measure(nameFont);

                if (w.Width < nameCard.Width)
                {
                    opt.Origin            = new Vector2(20, nameCard.Height * 2f / 3 + 10);
                    opt.VerticalAlignment = VerticalAlignment.Bottom;

                    nameCard.DrawText(opt, Username, Color.Black);

                    opt.Font              = rateFont;
                    opt.VerticalAlignment = VerticalAlignment.Top;

                    var ra = $"RATING: {r:00.00}";

                    nameCard.DrawText(opt, ra, Color.ParseHex("#1f1e33"));
                    break;
                }

                fontSize -= 2;
                nameFont =  new Font(consolas, fontSize, FontStyle.Bold);
            }

            // 称号（显示底分和段位）
            var rainbowCard = ResourceManager.GetImage("rainbow.png").ResizeX(nameCard.Width);

            nameFont = new Font(yaHei, 35, FontStyle.Bold);

            var text = $"B30 {B30:00.00} / R10 {R10:00.00}";

            rainbowCard.DrawTextCenter(text, nameFont, Color.Black);

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