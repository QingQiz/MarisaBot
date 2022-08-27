namespace Marisa.Plugin;

[MarisaPlugin]
[MarisaPluginNoDoc]
[MarisaPluginTrigger(typeof(EventHandler), nameof(Trigger))]
public class EventHandler : MarisaPluginBase
{
    public static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        return message.MessageChain!.Messages.Any(m => m.Type is
            MessageDataType.Nudge or
            MessageDataType.NewMember or 
            MessageDataType.MemberLeave
        );
    };

    [MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
    private MarisaPluginTaskState Handler(Message message, long qq)
    {
        var msg = message.MessageChain!.Messages.First(m => m.Type != MessageDataType.Id);

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (msg.Type)
        {
            // 戳一戳
            case MessageDataType.Nudge:
            {
                var m = (msg as MessageDataNudge)!;

                if (m.Target != qq) break;

                var word = ConfigurationManager.Configuration.Dirty.Take(31).ToList().RandomTake();

                if (ConfigurationManager.Configuration.Commander.Contains(m.FromId)) word = "别戳啦！";

                if (message.GroupInfo != null)
                {
                    message.Reply(
                        new MessageDataAt(m.FromId),
                        new MessageDataText(" "),
                        new MessageDataText(word)
                    );
                }
                else
                {
                    message.Reply(word);
                }

                break;
            }
            // 新成员
            case MessageDataType.NewMember:
            {
                var m = (msg as MessageDataNewMember)!;

                // 被人邀请
                if (m.InvitorId != null)
                {
                    message.Reply(new MessageDataAt((long)m.InvitorId),
                        new MessageDataText("邀请"),
                        new MessageDataAt(m.Id),
                        new MessageDataText("加入本群！欢迎！"));
                }
                else
                {
                    message.Reply(new MessageDataAt(m.Id), new MessageDataText("加入本群！欢迎！"));
                }

                break;
            }
            case MessageDataType.MemberLeave:
            {
                var m = (msg as MessageDataMemberLeave)!;

                if (m.Kicker == null)
                {
                    message.Reply($"{m.Name} ({m.Id}) 退群了");
                }
                else
                {
                    message.Reply(new MessageDataText($"{m.Name} ({m.Id}) 被"),
                        new MessageDataAt((long)m.Kicker),
                        new MessageDataText("踢了")
                    );
                }

                break;
            }
        }

        return MarisaPluginTaskState.CompletedTask;
    }
}