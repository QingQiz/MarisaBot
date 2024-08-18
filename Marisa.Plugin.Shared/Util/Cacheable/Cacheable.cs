namespace Marisa.Plugin.Shared.Util.Cacheable;

public abstract class Cacheable<T>
{
    private T? _value;

    protected Cacheable(string cacheFilePath, Func<T> create)
    {
        GetCacheFile = () => File.Exists(cacheFilePath) ? cacheFilePath : null;
        Get = () =>
        {
            if (GetCacheFile() is {} s)
            {
                CacheFilePath = s;
                return LoadCache(s);
            }

            var obj = create();

            CacheFilePath = cacheFilePath;
            SaveCache(cacheFilePath, obj);

            return obj;
        };
    }

    protected Cacheable(string cachePath, Func<string, bool> useCacheCondition, Func<T, string> otherwiseCreate, Func<T> create)
    {
        GetCacheFile = () => Directory.GetFiles(cachePath, "*.*", SearchOption.TopDirectoryOnly).FirstOrDefault(useCacheCondition);
        Get = () =>
        {
            if (GetCacheFile() is {} s)
            {
                CacheFilePath = s;
                return LoadCache(s);
            }

            var obj = create();

            CacheFilePath = Path.Join(cachePath, otherwiseCreate(obj));
            SaveCache(CacheFilePath, obj);

            return obj;
        };
    }

    protected Cacheable(string cachePath, Func<string, bool> useCacheCondition, string otherwiseCreate, Func<T> create)
        : this(cachePath, useCacheCondition, _ => otherwiseCreate, create)
    {
    }

    private Func<T> Get { get; }
    private Func<string?> GetCacheFile { get; }

    public T Value => _value ??= Get();
    public bool Available => GetCacheFile() is not null;

    public string? CacheFilePath { get; private set; }
    protected abstract T LoadCache(string path);
    protected abstract void SaveCache(string path, T t);
}