using Marisa.Plugin.Shared.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Ongeki;

public static class ResourceManager
{
    public static readonly string ResourcePath = ConfigurationManager.Configuration.Ongeki.ResourcePath;
    public static readonly string TempPath = ConfigurationManager.Configuration.Ongeki.TempPath;
    
    public static Image GetCover(long songId, bool resize = true)
    {
        var coverPath = ResourcePath + "/cover";

        var cp = $"{coverPath}/{songId}.png";

        if (!File.Exists(cp))
        {
            cp = $"{coverPath}/0.png";
        }

        var img = Image.Load(cp);

        if (resize)
        {
            img.Mutate(i => i.Resize(200, 200));
        }

        return img;
    }
}