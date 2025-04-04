using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Marisa.BotDriver.Extension;

public class WebContext
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, object>> ContextPool = new();

    public readonly Guid Id = Guid.NewGuid();
    public static bool DumpOnPut { get; set; }

    public static void Dump(Guid id, string name, object value)
    {
        var path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "WebContextHistory");

        Directory.CreateDirectory(path);

        var file = Path.Join(path, $"{name}.{id}");
        File.WriteAllText(file, JsonConvert.SerializeObject(value));
    }

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

        if (!DumpOnPut) return;

        Dump(Id, name, obj);
        Console.WriteLine($"Dump context {Id} to {Path.Join(AppDomain.CurrentDomain.BaseDirectory, "WebContextHistory", $"{name}.{Id}")}");
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