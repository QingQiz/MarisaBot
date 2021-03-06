using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Marisa.Utils;

public static class ImageDraw
{
    public static Bitmap Resize(this Image img, int width, int height)
    {
        var result = new Bitmap(width, height);

        using var g = Graphics.FromImage(result);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.HighQuality;

        g.DrawImage(img, 0, 0, width, height);

        return result;
    }

    public static Bitmap Resize(this Image img, double times)
    {
        var width  = img.Width * times;
        var height = img.Height * times;

        var result = new Bitmap((int)width, (int)height);

        using var g = Graphics.FromImage(result);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.HighQuality;

        g.DrawImage(img, 0, 0, (int)width, (int)height);

        return result;
    }

    public static Bitmap RoundCorners(this Image startImage, int cornerRadius)
    {
        cornerRadius *= 2;

        var roundedImage = new Bitmap(startImage.Width, startImage.Height);

        using (var g = Graphics.FromImage(roundedImage))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;

            Brush brush = new TextureBrush(startImage);
            var   gp    = new GraphicsPath();

            gp.AddArc(0, 0, cornerRadius, cornerRadius, 180, 90);
            gp.AddArc(0 + roundedImage.Width - cornerRadius, 0, cornerRadius, cornerRadius, 270, 90);
            gp.AddArc(0 + roundedImage.Width - cornerRadius, 0 + roundedImage.Height - cornerRadius, cornerRadius,
                cornerRadius, 0, 90);
            gp.AddArc(0, 0 + roundedImage.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
            g.FillPath(brush, gp);

            return roundedImage;
        }
    }

    public static Color CalculateAverageColor(this Bitmap bm, Rectangle rec)
    {
        var       width        = bm.Width;
        var       height       = bm.Height;
        const int minDiversion = -1;
        // keep track of dropped pixels
        var    dropped = 0;
        long[] totals  = { 0, 0, 0 };
        // cutting corners, will fail on anything else but 32 and 24 bit images
        var bppModifier = bm.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;

        var srcData = bm.LockBits(rec, ImageLockMode.ReadOnly, bm.PixelFormat);
        var stride  = srcData.Stride;
        var scan0   = srcData.Scan0;

        unsafe
        {
            var p = (byte*)(void*)scan0;

            for (var y = rec.Top; y < rec.Bottom; y++)
            {
                for (var x = rec.Left; x < rec.Right; x++)
                {
                    var idx   = y * stride + x * bppModifier;
                    int red   = p[idx + 2];
                    int green = p[idx + 1];
                    int blue  = p[idx];
                    if (Math.Abs(red - green) > minDiversion || Math.Abs(red - blue) > minDiversion ||
                        Math.Abs(green - blue) > minDiversion)
                    {
                        totals[2] += red;
                        totals[1] += green;
                        totals[0] += blue;
                    }
                    else
                    {
                        dropped++;
                    }
                }
            }
        }

        bm.UnlockBits(srcData);

        var count = rec.Width * rec.Height - dropped;
        count = count == 0 ? 1 : count;
        var avgR = (int)(totals[2] / count);
        var avgG = (int)(totals[1] / count);
        var avgB = (int)(totals[0] / count);

        return Color.FromArgb(avgR, avgG, avgB);
    }

    public static (Bitmap coverBackground, Color coverAvgColor) GetCoverBackground(this Bitmap cover)
    {
        var       width        = cover.Width;
        const int cornerRadius = 20;

        cover = Resize(cover, width, width);

        // 主题色
        var coverAvgColor = CalculateAverageColor(cover, new Rectangle(0, 0, 5, cover.Height));

        var coverBackground = new Bitmap(width * 2, width);

        var rec = new Rectangle(width, 0, width, width);

        using (var g = Graphics.FromImage(coverBackground))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;

            // 设置背景色
            g.Clear(coverAvgColor);
            // 贴上曲绘
            g.DrawImage(cover, width, 0);
            // 贴上渐变的主题色
            for (var i = rec.Left; i < rec.Right; i += 1)
            {
                var c = new SolidBrush(Color.FromArgb(255 - 255 * (i - rec.Left) / rec.Width, coverAvgColor));
                g.DrawLine(new Pen(c, 1), i, 0, i, rec.Height);
            }
        }

        // 圆角
        coverBackground = RoundCorners(coverBackground, cornerRadius);
        return (coverBackground, coverAvgColor);
    }

    public static Color SelectFontColor(this Color c)
    {
        var brightness = (int)Math.Sqrt(
            c.R * c.R * .241 +
            c.G * c.G * .691 +
            c.B * c.B * .068);
        return brightness > 130 ? Color.Black : Color.White;
    }

    public static Bitmap Copy(this Bitmap c)
    {
        return (Bitmap)c.Clone();
    }

    public static Bitmap GetStringCard(
        string text, int fontSize, Color fontColor, Color bgColor, int width,
        int height, int pl = 30, bool center = false, bool underLine = true)
    {
        var background = new Bitmap(width, height);

        using var g = Graphics.FromImage(background);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 调整字体大小
        var font = new Font("Consolas", fontSize);

        while (g.MeasureString(text, font).Width >= width - pl)
        {
            fontSize -= 2;
            font     =  new Font("Consolas", fontSize);
        }

        g.Clear(bgColor);

        var x = center
            ? (int)((width - g.MeasureString(text, font).Width) / 2)
            : pl;

        var y = (int)(height - g.MeasureString(text, font).Height) / 2;

        g.DrawString(string.IsNullOrEmpty(text) ? "-" : text, font, new SolidBrush(fontColor), x, y);

        if (underLine) g.DrawLine(new Pen(Color.Gray, 2), 0, height, width, height);

        return background;
    }

    public static string ToB64(this Bitmap bmp, long quality=90)
    {
        var ms = new MemoryStream();

        if (quality < 100)
        {
            var encoder = Encoder.Quality;  
      
            var encoderParameters = new EncoderParameters(1);  
            var parameter = new EncoderParameter(encoder, quality);  
            encoderParameters.Param[0] = parameter;  
      
            bmp.Save(ms, GetEncoder(ImageFormat.Jpeg), encoderParameters);
        }
        else
        {
            bmp.Save(ms, ImageFormat.Png);
        }

        return Convert.ToBase64String(ms.ToArray());
    }
    
    private static ImageCodecInfo GetEncoder(ImageFormat format)  
    {  
        var codecs = ImageCodecInfo.GetImageEncoders();
        return codecs.First(codec => codec.FormatID == format.Guid);
    }  

    public static unsafe Bitmap Blur(this Bitmap image, Rectangle rectangle, int blurSize)
    {
        var blurred = new Bitmap(image.Width, image.Height);

        // make an exact copy of the bitmap provided
        using (var graphics = Graphics.FromImage(blurred))
        {
            graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
        }

        // Lock the bitmap's bits
        var blurredData = blurred.LockBits(new Rectangle(0, 0, image.Width, image.Height),
            ImageLockMode.ReadWrite, blurred.PixelFormat);

        // Get bits per pixel for current PixelFormat
        var bitsPerPixel = Image.GetPixelFormatSize(blurred.PixelFormat);

        // Get pointer to first line
        var scan0 = (byte*)blurredData.Scan0.ToPointer();

        // look at every pixel in the blur rectangle
        for (var xx = rectangle.X; xx < rectangle.X + rectangle.Width; xx++)
        for (var yy = rectangle.Y; yy < rectangle.Y + rectangle.Height; yy++)
        {
            int avgR           = 0, avgG = 0, avgB = 0;
            var blurPixelCount = 0;

            // average the color of the red, green and blue for each pixel in the
            // blur size while making sure you don't go outside the image bounds
            for (var x = xx; x < xx + blurSize && x < image.Width; x++)
            for (var y = yy; y < yy + blurSize && y < image.Height; y++)
            {
                // Get pointer to RGB
                var data = scan0 + y * blurredData.Stride + x * bitsPerPixel / 8;

                avgB += data[0]; // Blue
                avgG += data[1]; // Green
                avgR += data[2]; // Red

                blurPixelCount++;
            }

            avgR /= blurPixelCount;
            avgG /= blurPixelCount;
            avgB /= blurPixelCount;

            // now that we know the average for the blur size, set each pixel to that color
            for (var x = xx; x < xx + blurSize && x < image.Width && x < rectangle.Width; x++)
            for (var y = yy; y < yy + blurSize && y < image.Height && y < rectangle.Height; y++)
            {
                // Get pointer to RGB
                var data = scan0 + y * blurredData.Stride + x * bitsPerPixel / 8;

                // Change values
                data[0] = (byte)avgB;
                data[1] = (byte)avgG;
                data[2] = (byte)avgR;
            }
        }

        // Unlock the bits
        blurred.UnlockBits(blurredData);

        return blurred;
    }

    public static Bitmap Crop(this Bitmap img, int x, int y, int width, int height)
    {
        return img.Crop(new Rectangle(x, y, width, height));
    }

    public static Bitmap Crop(this Bitmap img, Rectangle cropArea)
    {
        var m = new Bitmap(cropArea.Width, cropArea.Height);
        var g = Graphics.FromImage(m);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.HighQuality;

        g.DrawImage(img, new Rectangle(0, 0, m.Width, m.Height), cropArea, GraphicsUnit.Pixel);

        return m;
    }

    public static Bitmap RandomCut(this Bitmap img, int w, int h)
    {
        var rand = new Random();

        var x = rand.Next(0, img.Width - w);
        var y = rand.Next(0, img.Height - h);

        return img.Crop(new Rectangle(x, y, w, h));
    }

    public static Bitmap ResizeX(this Image im, int width)
    {
        var scale = (double)width / im.Width;
        return im.Resize(scale);
    }

    public static Bitmap ResizeY(this Image im, int height)
    {
        var scale = (double)height / im.Height;
        return im.Resize(scale);
    }
}