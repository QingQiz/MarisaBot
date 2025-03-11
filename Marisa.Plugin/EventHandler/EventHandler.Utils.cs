using Marisa.Plugin.Shared.Dialog;
using NLog;

namespace Marisa.Plugin.EventHandler;

public partial class EventHandler
{
    /// <summary>
    /// 戳一戳
    /// </summary>
    private static void NudgeHandler(Message message, MessageData msg, long qq)
    {
        var m = (msg as MessageDataNudge)!;

        if (m.Target != qq) return;

        const string word = "别戳啦！";

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
    }

    /// <summary>
    /// 新人入群
    /// </summary>
    private static void NewMemberHandler(Message message, MessageData msg, long qq)
    {
        var m = (msg as MessageDataNewMember)!;
        // 被人邀请
        if (m.InvitorId != null && m.InvitorId != 0)
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
    }

    /// <summary>
    /// 退群 / 被踢
    /// </summary>
    private static void MemberLeaveHandler(Message message, MessageData msg, long qq)
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
    }

    private static void BotMuteHandler(Message message, MessageData msg, long qq)
    {
        var m = (msg as MessageDataBotMute)!;

        var now = DateTime.Now;
        var log = LogManager.GetCurrentClassLogger();

        DialogManager.TryAddDialog((m.GroupId, null), message1 =>
        {
            // 超过了禁言时间
            if (DateTime.Now - now > m.Time)
            {
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            // 收到了解除禁言的消息
            if ((message1.MessageChain?.Messages.Any(md => md.Type == MessageDataType.BotUnmute) ?? false)
             && (message1.GroupInfo?.Id ?? 0) == m.GroupId)
            {
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            log.Warn("少女祈祷中...");

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        });
    }
}