using Marisa.Plugin.Shared.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ResourceManager
{
    public static readonly string ResourcePath = ConfigurationManager.Configuration.Chunithm.ResourcePath;
    public static readonly string TempPath = ConfigurationManager.Configuration.Chunithm.TempPath;

    public static Image GetCover(long songId, bool resize = true)
    {
        var coverPath = ResourcePath + "/cover";

        var cp = $"{coverPath}/{songId}.png";

        var img = Image.Load(cp);

        if (resize)
        {
            img.Mutate(i => i.Resize(200, 200));
        }

        return img;
    }
}