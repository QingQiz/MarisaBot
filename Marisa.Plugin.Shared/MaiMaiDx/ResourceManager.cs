using Marisa.Plugin.Shared.Configuration;
using Marisa.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public static class ResourceManager
{
    public static readonly string ResourcePath = ConfigurationManager.Configuration.MaiMai.ResourcePath;
    public static readonly string TempPath = ConfigurationManager.Configuration.MaiMai.TempPath;

    public static Image GetImage(string imgName, double scale)
    {
        var img = GetImage(imgName).Resize(scale);

        return img;
    }

    public static Image GetImage(string imgName, int width = 0, int height = 0)
    {
        var imgPath = ResourcePath + "/pic";
        var img     = Image.Load($"{imgPath}/{imgName}");

        if (width != 0 && height != 0)
        {
            img.Mutate(i => i.Resize(width, height));
        }

        return img;
    }

    public static Image GetCover(long songId, bool resize = true)
    {
        var coverPath = ResourcePath + "/cover";

        var cp = $"{coverPath}/{songId}.png";

        if (!File.Exists(cp))
        {
            cp = cp[..^3] + "jpg";
        }

        if (!File.Exists(cp))
        {
            // ReSharper disable once TailRecursiveCall
            return GetCover(1000, resize);
        }

        var img = Image.Load(cp)!;

        if (resize)
        {
            img.Mutate(i => i.Resize(200, 200));
        }

        return img;
    }

    public static (Image, Color) GetCoverBackground(long songId)
    {
        const string prefix = "CoverBackground-";

        var x = Directory.GetFiles(TempPath, $"{prefix}*.png", SearchOption.TopDirectoryOnly).FirstOrDefault(x => x.StartsWith(prefix) && x.EndsWith($"-{songId}.png"));

        if (x == null)
        {
            var (bg, color) = GetCover(songId).GetCoverBackground();

            Task.Run(() => { bg.SaveAsPng(Path.Join(TempPath, prefix) + "-#" + color.ToHex() + $"-{songId}.png"); });

            return (bg.CloneAs<Rgba32>(), color);
        }
        else
        {
            var color = Color.ParseHex(x.Split("-")[1]);
            return (Image.Load(x), color);
        }
    }
}