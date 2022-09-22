using Marisa.Plugin.Shared.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ResourceManager
{
    public static readonly string ResourcePath = ConfigurationManager.Configuration.Chunithm.ResourcePath;
    public static readonly string TempPath = ConfigurationManager.Configuration.Chunithm.TempPath;

    // public static Image GetImage(string imgName, double scale)
    // {
    //     var img = GetImage(imgName).Resize(scale);
    //
    //     return img;
    // }

    // public static Image GetImage(string imgName, int width = 0, int height = 0)
    // {
    //     var imgPath = ResourcePath + "/pic";
    //     var img     = Image.Load($"{imgPath}/{imgName}");
    //
    //     if (width != 0 && height != 0)
    //     {
    //         img.Mutate(i => i.Resize(width, height));
    //     }
    //
    //     return img;
    // }

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

    // public static (Image, Color) GetCoverBackground(long songId)
    // {
    //     return GetCover(songId).GetCoverBackground();
    // }
}