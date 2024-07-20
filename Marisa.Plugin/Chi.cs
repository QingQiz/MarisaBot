namespace Marisa.Plugin;

[MarisaPluginDoc("这是一个用来解决「中午吃什么」这一人生 N 大难题之一的功能")]
[MarisaPluginCommand(true, "吃啥", "吃什么")]
public class Chi : MarisaPluginBase
{
    private readonly Dictionary<long, (DateTime, int)> _cache = new();
    private const int Times = 5;

    private bool Zuo(long id)
    {
        lock (_cache)
        {
            if (_cache.ContainsKey(id))
            {
                var (time, t) = _cache[id];
                if (DateTime.Now - time < TimeSpan.FromMinutes(5))
                {
                    if (t >= Times) return true;

                    _cache[id] = (DateTime.Now, t + 1);
                    return false;
                }

                _cache[id] = (DateTime.Now, 1);
                return false;
            }

            _cache[id] = (DateTime.Now, 1);
            return false;
        }
    }

    private string ChiSha(long id)
    {
        return Zuo(id) ? "生吃你妈 问这么多还不知道吃啥饿死你个臭傻逼" : ConfigurationManager.Configuration.Chi.RandomTake();
    }

    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private MarisaPluginTaskState Proc(Message message)
    {
        var sender = message.Sender.Id;

        message.Reply(ChiSha(sender));

        return MarisaPluginTaskState.CompletedTask;
    }
}