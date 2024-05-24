using Marisa.Backend.Mirai.MessageDataExt;
using NLog;

namespace Marisa.Plugin.EventHandler;

[MarisaPlugin]
[MarisaPluginNoDoc]
[MarisaPluginTrigger(typeof(EventHandler), nameof(Trigger))]
public partial class EventHandler : MarisaPluginBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly Debounce SignServerKillerDebounce = new(1000, 5000);
    private static readonly Debounce BotLoginDebounce = new(5000, 1000);

    public static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        return message.MessageChain!.Messages.Any(m => m.Type is
            MessageDataType.Nudge or
            MessageDataType.NewMember or MessageDataType.MemberLeave or
            MessageDataType.BotMute or MessageDataType.BotUnmute
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
            case MessageDataType.BotOffline:
                Logger.Warn("Bot offline unexpectedly, try to login again.");
                BotLoginDebounce.Execute(() => driver.Login().Wait());
                break;
            case MessageDataType.BotOnline:
                Logger.Warn("Bot online successfully.");
                BotLoginDebounce.Cancel();
                break;
            case MessageDataType.Unknown when msg is MessageDataSignServerLose:
            {
                Logger.Warn("Lose connection to SingServer");

                SignServerKillerDebounce.Execute(KillSignServer);
                break;
            }
        }

        return MarisaPluginTaskState.CompletedTask;
    }
}