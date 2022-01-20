using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPluginCommand(true, "吃啥", "吃什么")]
[MiraiPluginTrigger(typeof(MiraiPluginTrigger), nameof(MiraiPluginTrigger.PlainTextTrigger))]
public class Chi : MiraiPluginBase
{
    private const string F =
        "下苍蝇馆子 下豪华馆子 汉堡王 绿茶 烤肉 兰州拉面 老碗面 必胜客 麦当劳 食堂随便吃点 馄饨 拉面 刀削面 油泼面 炸酱面 炒面 重庆小面 米线 酸辣粉 土豆粉 凉皮儿 麻辣烫 炒饭 盖浇饭 烤肉饭 黄焖鸡米饭 麻辣香锅 火锅 烤串 生煎 屎";

    private readonly Dictionary<long, (DateTime, int)> _cache = new();
    private const int Times = 5;

    private bool Trigger(Message message)
    {
        var txt = message.MessageChain!.PlainText.Trim();

        return txt.Equals("吃什么") || txt.Equals("吃啥");
    }

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
        if (Zuo(id)) return "生吃你妈 问这么多还不知道吃啥饿死你个臭傻逼";

        var f = F.Split(' ');
        var r = new Random().Next(0, f.Length);
        return f[r];
    }

    [MiraiPluginCommand]
    private MiraiPluginTaskState Proc(MessageSenderProvider ms, Message message)
    {
        var sender = message.Sender!.Id;
        var toSend = MessageChain.FromPlainText(ChiSha(sender));

        ms.Reply(toSend, message);

        return MiraiPluginTaskState.CompletedTask;
    }
}