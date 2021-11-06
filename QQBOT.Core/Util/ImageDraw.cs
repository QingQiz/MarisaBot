using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace QQBOT.Core.Util
{
    public static class ImageDraw
    {
        public static Bitmap Resize(this Image img, int width, int height)
        {
            var result = new Bitmap(width, height);

            using var g = Graphics.FromImage(result);

            g.DrawImage(img, 0, 0, width, height);

            return result;
        }

        public static Bitmap Resize(this Bitmap img, double times)
        {
            var width  = img.Width  * times;
            var height = img.Height * times;

            var result = new Bitmap((int)width, (int)height);

            using var g = Graphics.FromImage(result);

            g.DrawImage(img, 0, 0, (int)width, (int)height);

            return result;
        }

        public static Bitmap RoundCorners(this Image startImage, int cornerRadius)
        {
            cornerRadius *= 2;

            var roundedImage = new Bitmap(startImage.Width, startImage.Height);

            using (var g = Graphics.FromImage(roundedImage))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Brush brush = new TextureBrush(startImage);
                var gp = new GraphicsPath();

                gp.AddArc(0, 0, cornerRadius, cornerRadius, 180, 90);
                gp.AddArc(0 + roundedImage.Width - cornerRadius, 0, cornerRadius, cornerRadius, 270, 90);
                gp.AddArc(0 + roundedImage.Width - cornerRadius, 0 + roundedImage.Height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
                gp.AddArc(0, 0 + roundedImage.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
                g.FillPath(brush, gp);

                return roundedImage;
            }
        }

        private static Color CalculateAverageColor(this Bitmap bm)
        {
            var width = bm.Width;
            var height = bm.Height;
            const int minDiversion = 15;
            // keep track of dropped pixels
            var dropped = 0;
            long[] totals = { 0, 0, 0 };
            // cutting corners, will fail on anything else but 32 and 24 bit images
            var bppModifier = bm.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;

            var srcData = bm.LockBits(new Rectangle(0, 0, bm.Width, bm.Height), ImageLockMode.ReadOnly, bm.PixelFormat);
            var stride = srcData.Stride;
            var scan0 = srcData.Scan0;

            unsafe
            {
                var p = (byte*)(void*)scan0;

                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var idx = y * stride + x * bppModifier;
                        int red = p![idx  + 2];
                        int green = p[idx + 1];
                        int blue = p[idx];
                        if (Math.Abs(red   - green) > minDiversion || Math.Abs(red - blue) > minDiversion ||
                            Math.Abs(green - blue)  > minDiversion)
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

            var count = width * height - dropped;
            var avgR = (int)(totals[2] / count);
            var avgG = (int)(totals[1] / count);
            var avgB = (int)(totals[0] / count);

            return Color.FromArgb(avgR, avgG, avgB);
        }

        public static (Bitmap coverBackground, Color coverAvgColor) GetCoverBackground(this Bitmap cover)
        {
            var width = cover.Width;
            const int cornerRadius = 20;
            
            cover = Resize(cover, width, width);

            // 主题色
            var coverAvgColor = CalculateAverageColor(cover);

            var coverBackground = new Bitmap(width * 2, width);

            var coverRect = new Rectangle(0, 0, width, width);

            var gradiantCoverColorBrush = new LinearGradientBrush(coverRect, coverAvgColor, Color.Transparent,
                LinearGradientMode.Horizontal);

            using (var g = Graphics.FromImage(coverBackground))
            {
                // 设置背景色
                g.Clear(coverAvgColor);
                // 贴上曲绘
                g.DrawImage(cover, width, 0);
                // 贴上渐变的主题色
                g.FillRectangle(gradiantCoverColorBrush, new Rectangle(width, 0, width, width));
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
    }
}