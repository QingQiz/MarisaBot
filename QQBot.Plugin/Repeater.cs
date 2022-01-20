using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPlugin(-20)]
[MiraiPluginCommand(MiraiMessageType.GroupMessage)]
[MiraiPluginTrigger(typeof(MiraiPluginTrigger), nameof(MiraiPluginTrigger.PlainTextTrigger), MiraiMessageType.GroupMessage)]
public class Repeater: MiraiPluginBase
{
    private static readonly Dictionary<long, (string m, int t)> RepeaterStatus = new();
    private static readonly (int fst, int snd) ThreshHold = (3, 9);

    [MiraiPluginCommand]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider ms)
    {
        if (!RepeaterStatus.ContainsKey(m.Location))
        {
            RepeaterStatus[m.Location] = (m.Command, 1);
            return MiraiPluginTaskState.NoResponse;
        }

        var s = RepeaterStatus[m.Location];

        if (m.Command == s.m)
        {
            s.t += 1;
        }
        else
        {
            s = (m.Command, 1);
        }

        RepeaterStatus[m.Location] = s;

        if (s.t == ThreshHold.fst)
        {
            ms.Reply(m.Command, m, false);
        }

        if (s.t == ThreshHold.snd)
        {
            ms.Reply("复读你🐴呢", m, false);
        }

        return MiraiPluginTaskState.NoResponse;
    }
}