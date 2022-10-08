namespace Marisa.Utils.Cacheable;

public abstract class Cacheable<T>
{
    protected abstract T LoadCache(string path);
    protected abstract void SaveCache(string path, T t);

    private Func<T> Get { get; }
    private T? _value;

    public T Value => _value ??= Get();
    public string? CacheFilePath { get; private set; }

    protected Cacheable(string cacheFilePath, Func<T> create)
    {
        Get = () =>
        {
            if (File.Exists(cacheFilePath))
            {
                CacheFilePath = cacheFilePath;
                return LoadCache(cacheFilePath);
            }

            var obj = create();

            CacheFilePath = cacheFilePath;
            SaveCache(cacheFilePath, obj);

            return obj;
        };
    }

    protected Cacheable(string cachePath, Func<string, bool> useCacheCondition, Func<T, string> otherwiseCreate, Func<T> create)
    {
        Get = () =>
        {
            foreach (var f in Directory.GetFiles(cachePath, "*.*", SearchOption.TopDirectoryOnly))
            {
                var canUse = useCacheCondition(f);

                if (canUse)
                {
                    CacheFilePath = f;
                    return LoadCache(f);
                }
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
}