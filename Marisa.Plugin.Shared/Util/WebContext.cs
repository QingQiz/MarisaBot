using System.Collections.Concurrent;
using Marisa.Configuration;
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Util;

public class WebContext
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, object>> ContextPool = new();
    private const string HistoryDirectoryName = "WebContextHistory";

    public readonly Guid Id = Guid.NewGuid();
    public static bool DumpOnPut { get; set; }

    public static string GetHistoryPath()
    {
        return Path.Join(ConfigurationManager.Configuration.TempPath, HistoryDirectoryName);
    }

    public static string EnsureHistoryPath()
    {
        var path = GetHistoryPath();
        Directory.CreateDirectory(path);
        return path;
    }

    public static void Dump(Guid id, string name, object value)
    {
        var path = EnsureHistoryPath();

        var file = Path.Join(path, $"{name}.{id}");
        File.WriteAllText(file, SerializeForStorage(value));
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
        Console.WriteLine($"Dump context {Id} to {Path.Join(GetHistoryPath(), $"{name}.{Id}")}");
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

    private static string SerializeForStorage(object value)
    {
        return value is string str ? str : JsonConvert.SerializeObject(value);
    }
}
