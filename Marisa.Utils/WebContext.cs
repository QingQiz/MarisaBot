using System.Collections.Concurrent;

namespace Marisa.Utils;

public class WebContext
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, object>> ContextPool = new();

    public readonly Guid Id = Guid.NewGuid();

    public WebContext()
    {
        ContextPool.TryAdd(Id, new ConcurrentDictionary<string, object>());
    }

    public void Put(string name, object obj)
    {
        if (ContextPool.TryGetValue(Id, out var context))
        {
            context.TryAdd(name, obj);
        }
    }

    public static object Get(Guid id, string name)
    {
        if (ContextPool.TryGetValue(id, out var context))
        {
            if (context.TryGetValue(name, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"{name} not found in context {id}");
        }
        throw new KeyNotFoundException($"Context {id} not found");
    }

    public object Get(string name)
    {
        return Get(Id, name);
    }

    public bool Contains(string name)
    {
        return ContextPool.TryGetValue(Id, out var context) && context.ContainsKey(name);
    }

    ~WebContext()
    {
        ContextPool.TryRemove(Id, out _);
    }
}