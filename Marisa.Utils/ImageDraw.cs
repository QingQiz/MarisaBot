using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Marisa.Utils;

public static class ImageDraw
{
    public static Image RoundCorners(this Image image, int cornerRadius)
    {
        image.Mutate(i => i.RoundCorners(cornerRadius));
        return image;
    }

    public static IPathCollection BuildCorners(int imageWidth, int imageHeight, float cornerRadius)
    {
        var rect = new RectangularPolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

        var cornerTopLeft = rect.Clip(new EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

        var rightPos  = imageWidth - cornerTopLeft.Bounds.Width + 1;
        var bottomPos = imageHeight - cornerTopLeft.Bounds.Height + 1;

        var cornerTopRight    = cornerTopLeft.RotateDegree(90).Translate(rightPos, 0);
        var cornerBottomLeft  = cornerTopLeft.RotateDegree(-90).Translate(0, bottomPos);
        var cornerBottomRight = cornerTopLeft.RotateDegree(180).Translate(rightPos, bottomPos);

        return new PathCollection(cornerTopLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
    }

    public static IImageProcessingContext RoundCorners(this IImageProcessingContext ctx, int cornerRadius)
    {
        var size    = ctx.GetCurrentSize();
        var corners = BuildCorners(size.Width, size.Height, cornerRadius);

        ctx.SetGraphicsOptions(new GraphicsOptions
        {
            // enforces that any part of this shape that has color is punched out of the background
            AlphaCompositionMode = PixelAlphaCompositionMode.DestOut
        });

        var graphicsOptions = new GraphicsOptions
        {
            AlphaCompositionMode = PixelAlphaCompositionMode.Src,
            ColorBlendingMode    = PixelColorBlendingMode.Normal
        };

        return ctx.Fill(new DrawingOptions { GraphicsOptions = graphicsOptions }, Color.Transparent, corners);
    }

    public static Color DominantColor(this Image image, Rectangle rectangle)
    {
        var rgba32 = image.Clone(i => i
            .Crop(rectangle)
            .Quantize(new OctreeQuantizer(new QuantizerOptions()
            {
                MaxColors = 1
            }))
        ).CloneAs<Rgba32>()[0, 0];

        return Color.FromRgba(rgba32.R, rgba32.G, rgba32.B, rgba32.A);
    }

    public static (Image CoverBackground, Color CoverDominantColor) GetCoverBackground(this Image cover)
    {
        var       width        = cover.Width;
        const int cornerRadius = 20;

        cover.Mutate(i => i.Resize(width, width));

        // 主题色
        var coverDominantColor = cover.DominantColor(new Rectangle(0, 0, 5, cover.Height));
        var colorPixel         = coverDominantColor.ToPixel<Rgba32>();

        var coverBackground = new Image<Rgba32>(width * 2, width);

        coverBackground.Mutate(i => i
            // 设置背景色
            .Fill(coverDominantColor)
            // 贴上曲绘
            .DrawImage(cover, width, 0)
        );

        // 贴上渐变的主题色
        var rec = new Rectangle(width, 0, width, width);
        for (var i = rec.Left; i < rec.Right; i += 1)
        {
            var i1 = i;
            var c  = Color.FromRgba(colorPixel.R, colorPixel.G, colorPixel.B, (byte)(255 - 255 * (i - rec.Left) / rec.Width));

            coverBackground.Mutate(ctx => ctx
                .DrawLines(new Pen(c, 1), new PointF(i1, 0), new PointF(i1, rec.Height)));
        }

        // 圆角
        coverBackground.Mutate(i => i.RoundCorners(cornerRadius));

        return (coverBackground, coverDominantColor);
    }

    public static Color SelectFontColor(this Color c)
    {
        var color = c.ToPixel<Rgba32>();

        var brightness = (int)Math.Sqrt(
            color.R * color.R * .241 +
            color.G * color.G * .691 +
            color.B * color.B * .068);
        return brightness > 130 ? Color.Black : Color.White;
    }

    public static Image Clear(this Image image, Color color)
    {
        image.Mutate(i => i.Fill(color));
        return image;
    }

    public static Image GetStringCard(
        string text, int fontSize, Color fontColor, Color bgColor, int width,
        int height, int paddingLeft = 30, bool center = false, bool underLine = true)
    {
        var background = new Image<Rgba32>(width, height);

        // 调整字体大小
        var fontFamily = SystemFonts.Get("Consolas");

        var font = new Font(fontFamily, fontSize);

        text = string.IsNullOrWhiteSpace(text) ? "N/A" : text;

        while (text.Measure(font).Width >= width - paddingLeft)
        {
            fontSize -= 2;
            font     =  new Font(fontFamily, fontSize);
        }

        var measure = text.Measure(font);

        var x = center ? (int)((width - measure.Width) / 2) : paddingLeft;
        var y = (int)(height - measure.Height) / 2;

        background.Mutate(i => i
            .Fill(bgColor)
            .DrawText(text, font, fontColor, x, y)
        );

        if (underLine)
        {
            background.Mutate(i =>
                i.DrawLines(new Pen(Color.Gray, 2), new PointF(0, height), new PointF(width, height)));
        }

        return background;
    }

    #region Draw Image

    public static IImageProcessingContext DrawImage(this IImageProcessingContext ctx, Image image, int x, int y, float opacity = 1)
    {
        return ctx.DrawImage(image, new Point(x, y), opacity);
    }

    public static Image DrawImage(this Image image, Image toDraw, int x, int y, float opacity = 1)
    {
        image.Mutate(i => i.DrawImage(toDraw, x, y, opacity));
        return image;
    }

    #endregion

    #region Draw Text

    public static Image DrawText(this Image img, string text, FontFamily fontFamily, int fontSize, Color color, float x, float y)
    {
        img.Mutate(i => i.DrawText(text, fontFamily, fontSize, color, x, y));
        return img;
    }

    public static Image DrawText(this Image img, string text, Font font, Color color, float x, float y)
    {
        img.Mutate(i => i.DrawText(text, font, color, x, y));
        return img;
    }

    public static IImageProcessingContext DrawText(
        this IImageProcessingContext ctx, string text, FontFamily fontFamily, int fontSize, Color color, float x, float y)
    {
        return ctx.DrawText(text, fontFamily.CreateFont(fontSize), color, x, y);
    }

    public static TextOptions GetTextOptions(Font font)
    {
        return new TextOptions(font)
        {
            FallbackFontFamilies = new[]
            {
                SystemFonts.Get("FangSong"),
                SystemFonts.Get("NSimSun"),
                SystemFonts.Get("SimSun"),
                SystemFonts.Get("Microsoft JhengHei"),
            },
        };
    }

    public static TextOptions GetTextOptions(Font font, PointF location)
    {
        var option = GetTextOptions(font);

        option.Origin = location;

        return option;
    }

    public static IImageProcessingContext DrawText(this IImageProcessingContext ctx, string text, Font font, Color color, float x, float y)
    {
        return ctx.DrawText(GetTextOptions(font, new PointF(x, y)), text, color);
    }

    #endregion

    #region Image Cut

    public static IImageProcessingContext RandomCut(this IImageProcessingContext ctx, int w, int h)
    {
        var rand = new Random();

        var size = ctx.GetCurrentSize();

        var x = rand.Next(0, size.Width - w);
        var y = rand.Next(0, size.Height - h);

        return ctx.Crop(new Rectangle(x, y, w, h));
    }

    public static Image Crop(this Image image, int x, int y, int w, int h)
    {
        image.Mutate(i => i.Crop(new Rectangle(x, y, w, h)));
        return image;
    }

    #endregion

    #region Resize

    public static Image Resize(this Image image, int width, int height)
    {
        image.Mutate(i => i.Resize(width, height));
        return image;
    }

    public static Image Resize(this Image image, double scale)
    {
        image.Mutate(i => i.Resize(scale));
        return image;
    }

    public static IImageProcessingContext Resize(this IImageProcessingContext ctx, double scale)
    {
        var size = ctx.GetCurrentSize();

        var width  = size.Width * scale;
        var height = size.Height * scale;

        return ctx.Resize((int)width, (int)height);
    }

    public static Image ResizeX(this Image image, int width)
    {
        image.Mutate(i => i.ResizeX(width));
        return image;
    }

    public static Image ResizeY(this Image image, int height)
    {
        image.Mutate(i => i.ResizeY(height));
        return image;
    }


    public static IImageProcessingContext ResizeX(this IImageProcessingContext ctx, int width)
    {
        var size  = ctx.GetCurrentSize();
        var scale = (double)width / size.Width;

        return ctx.Resize(scale);
    }

    public static IImageProcessingContext ResizeY(this IImageProcessingContext ctx, int height)
    {
        var size  = ctx.GetCurrentSize();
        var scale = (double)height / size.Height;

        return ctx.Resize(scale);
    }

    #endregion

    #region Converter

    public static Image<TPixel> ToImageSharpImage<TPixel>(this System.Drawing.Bitmap bitmap) where TPixel : unmanaged, IPixel<TPixel>
    {
        using var memoryStream = new MemoryStream();

        bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);

        memoryStream.Seek(0, SeekOrigin.Begin);

        return Image.Load<TPixel>(memoryStream);
    }

    public static string ToB64(this Image image, int quality = 90)
    {
        var ms = new MemoryStream();

        if (quality < 100)
        {
            var encoder = new JpegEncoder
            {
                Quality = quality
            };

            image.SaveAsJpeg(ms, encoder);
        }
        else
        {
            image.SaveAsPng(ms);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    #endregion
}