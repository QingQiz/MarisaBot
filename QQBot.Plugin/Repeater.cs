using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared;

namespace QQBot.Plugin;

[MiraiPlugin(PluginPriority.Repeater)]
[MiraiPluginCommand(MiraiMessageType.GroupMessage)]
public class Repeater: MiraiPluginBase
{
    private static readonly Dictionary<long, (string m, int t)> RepeaterStatus = new();
    private static readonly (int fst, int snd) ThreshHold = (3, 9);

    [MiraiPluginCommand]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider ms)
    {
        lock (RepeaterStatus)
        {
            if (!m.MessageChain!.Messages.All(msg => msg.Type is MessageType.Source or MessageType.Plain) ||
                m.MessageChain.Messages.Count <= 1)
            {
                RepeaterStatus.Remove(m.Location);
                return MiraiPluginTaskState.NoResponse;
            }

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
}