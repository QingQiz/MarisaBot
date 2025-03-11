namespace Marisa.Plugin.EventHandler;

[MarisaPlugin]
[MarisaPluginNoDoc]
[MarisaPluginTrigger(typeof(EventHandler), nameof(Trigger))]
public partial class EventHandler : MarisaPluginBase
{
    public static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        return message.MessageChain!.Messages.Any(m =>
            m.Type is
                MessageDataType.Nudge
             or MessageDataType.NewMember
             or MessageDataType.MemberLeave
             or MessageDataType.BotMute
             or MessageDataType.BotUnmute
             or MessageDataType.BotOffline
             or MessageDataType.BotOnline
         || m is MessageDataSignServerLose
        );
    };

    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
    private MarisaPluginTaskState Handler(Message message, long qq, BotDriver.BotDriver driver)
    {
        var msg = message.MessageChain!.Messages.First(m => m.Type != MessageDataType.Id);

        void InvokeHandler(Action<Message, MessageData, long> handler)
        {
            handler(message, msg, qq);
        }

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (msg.Type)
        {
            case MessageDataType.Nudge:
                InvokeHandler(NudgeHandler);
                break;
            case MessageDataType.NewMember:
                InvokeHandler(NewMemberHandler);
                break;
            case MessageDataType.MemberLeave:
                InvokeHandler(MemberLeaveHandler);
                break;
            case MessageDataType.BotMute:
                InvokeHandler(BotMuteHandler);
                break;
        }

        return MarisaPluginTaskState.CompletedTask;
    }
}