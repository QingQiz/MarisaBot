using System.Collections.Concurrent;

namespace Marisa.Backend.Shared;

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
                context.TryRemove(name, out _);

                if (context.IsEmpty)
                {
                    ContextPool.TryRemove(id, out _);
                }

                return value;
            }
            throw new KeyNotFoundException($"{name} not found in context {id}");
        }
        throw new KeyNotFoundException($"Context {id} not found");
    }
}