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
    private static (string m, int t) _repeaterStatus = (null, 0)!;
    private static readonly (int fst, int snd) ThreshHold = (3, 9);

    [MiraiPluginCommand]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider ms)
    {
        if (m.Command == _repeaterStatus.m)
        {
            _repeaterStatus.t++;
        }
        else
        {
            _repeaterStatus= (m.Command, 1);
        }

        if (_repeaterStatus.t == ThreshHold.fst)
        {
            ms.Reply(m.Command, m, false);
        }

        if (_repeaterStatus.t == ThreshHold.snd)
        {
            ms.Reply("复读你🐴呢", m, false);
        }

        return MiraiPluginTaskState.NoResponse;
    }
}