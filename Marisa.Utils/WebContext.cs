namespace Marisa.Utils;

public class WebContext
{
    private static readonly Dictionary<Guid, Dictionary<string, object>> ContextPool = new();

    public readonly Guid Id = Guid.NewGuid();

    public WebContext()
    {
        ContextPool.Add(Id, new Dictionary<string, object>());
    }

    public void Put(string name, object obj)
    {
        ContextPool[Id].Add(name, obj);
    }

    public static object Get(Guid id, string name)
    {
        return ContextPool[id][name];
    }

    public object Get(string name)
    {
        return Get(Id, name);
    }

    public bool Contains(string name)
    {
        return ContextPool[Id].ContainsKey(name);
    }

    ~WebContext()
    {
        ContextPool.Remove(Id);
    }
}