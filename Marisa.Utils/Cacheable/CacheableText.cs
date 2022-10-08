namespace Marisa.Utils.Cacheable;

public class CacheableText : Cacheable<string>
{
    protected override string LoadCache(string path)
    {
        return File.ReadAllText(path);
    }

    protected override void SaveCache(string path, string t)
    {
        File.WriteAllText(path, t);
    }

    public CacheableText(string cacheFilePath, Func<string> create) : base(cacheFilePath, create)
    {
    }

    public CacheableText(string cachePath, Func<string, bool> useCacheCondition, Func<string, string> otherwiseCreate, Func<string> create) : base(cachePath, useCacheCondition, otherwiseCreate, create)
    {
    }

    public CacheableText(string cachePath, Func<string, bool> useCacheCondition, string otherwiseCreate, Func<string> create) : base(cachePath, useCacheCondition, otherwiseCreate, create)
    {
    }
}