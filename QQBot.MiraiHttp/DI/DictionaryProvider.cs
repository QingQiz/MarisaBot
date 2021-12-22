namespace QQBot.MiraiHttp.DI;

public class DictionaryProvider
{
    private readonly Dictionary<string, dynamic> _dictionary = new();

    public DictionaryProvider()
    {
    }

    public bool ContainsKey(string key) => _dictionary.ContainsKey(key);

    public dynamic this[string key]
    {
        get => _dictionary[key];
        set
        {
            lock (_dictionary)
            {
                _dictionary[key] = value;
            }
        }
    }

    public T Get<T>(string key)
    {
        return (T)this[key];
    }

    public dynamic Get(string key)
    {
        return this[key];
    }

    public DictionaryProvider Add(string key, dynamic value)
    {
        this[key] = value;
        return this;
    }
}