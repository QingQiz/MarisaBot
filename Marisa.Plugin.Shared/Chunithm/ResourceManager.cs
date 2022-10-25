using Marisa.Plugin.Shared.Configuration;
using Marisa.Utils;
using Marisa.Utils.Cacheable;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ResourceManager
{
    public static readonly string ResourcePath = ConfigurationManager.Configuration.Chunithm.ResourcePath;
    public static readonly string TempPath = ConfigurationManager.Configuration.Chunithm.TempPath;

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
        else if (width != 0)
        {
            img = img.ResizeX(width);
        }
        else if (height != 0)
        {
            img = img.ResizeY(height);
        }

        return img;
    }

    public static Image GetCover(long songId, bool resize = true)
    {
        var coverPath = ResourcePath + "/cover";

        var cp = $"{coverPath}/{songId}.png";

        var img = Image.Load(cp)!;

        if (resize)
        {
            img.Mutate(i => i.Resize(200, 200));
        }

        return img;
    }

    public static (Image, Color) GetCoverBackground(long songId)
    {
        var prefix = $"CoverBackground-{songId}-";

        var image = new CacheableImage(TempPath,
            f => f.StartsWith(prefix) && f.EndsWith(".png"),
            cover => $"{prefix}#{cover.DominantColor(new Rectangle(0, 0, 5, cover.Height)).ToHex()}.png",
            () => GetCover(songId).GetCoverBackground()
        );

        var ret   = image.Value;
        var color = Color.ParseHex(Path.GetFileNameWithoutExtension(image.CacheFilePath)!.Split('-').Last());
        return (ret, color);
    }
}