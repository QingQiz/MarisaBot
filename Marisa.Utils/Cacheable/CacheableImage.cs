using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Marisa.Utils.Cacheable;

public class CacheableImage : Cacheable<Image>
{
    protected override Image LoadCache(string path)
    {
        return Image.Load(path).CloneAs<Rgba32>();
    }

    protected override void SaveCache(string path, Image t)
    {
        var save = t.CloneAs<Rgba32>();
        Task.Run(() => save.SaveAsPng(path));
    }

    public CacheableImage(string cacheFilePath, Func<Image> create) : base(cacheFilePath, create)
    {
    }

    public CacheableImage(string cachePath, Func<string, bool> useCacheCondition, Func<Image, string> otherwiseCreate, Func<Image> create) : base(cachePath, useCacheCondition, otherwiseCreate, create)
    {
    }

    public CacheableImage(string cachePath, Func<string, bool> useCacheCondition, string otherwiseCreate, Func<Image> create) : base(cachePath, useCacheCondition, otherwiseCreate, create)
    {
    }
}