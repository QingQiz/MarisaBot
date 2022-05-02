namespace Marisa.BotDriver.DI;

/// <summary>
/// 提供一个字典，忽略大小写
/// </summary>
public class DictionaryProvider
{
    private readonly Dictionary<string, dynamic> _dictionary = new();

    public DictionaryProvider()
    {
    }

    public bool ContainsKey(string key) => _dictionary.ContainsKey(key.ToLower());

    public dynamic this[string key]
    {
        get => _dictionary[key.ToLower()];
        set
        {
            lock (_dictionary)
            {
                _dictionary[key.ToLower()] = value;
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