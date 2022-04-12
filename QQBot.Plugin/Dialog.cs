using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared;

namespace QQBot.Plugin;

[MiraiPlugin(PluginPriority.Dialog)]
[MiraiPluginCommand(MiraiMessageType.GroupMessage | MiraiMessageType.FriendMessage)]
public class Dialog : MiraiPluginBase
{
    // map (group id, user id) to handler
    private static readonly Dictionary<(long?, long?), Shared.Dialog.Dialog.MessageHandler> Handlers = new();

    public static bool AddHandler(long? groupId, long? senderId, Shared.Dialog.Dialog.MessageHandler handler)
    {
        lock (Handlers)
        {
            if (Handlers.ContainsKey((groupId, senderId))) return false;

            Handlers[(groupId, senderId)] = handler;
            return true;
        }
    }

    [MiraiPluginCommand]
    private static MiraiPluginTaskState MessageHandler(MessageSenderProvider sender, Message message)
    {
        var groupId  = message.GroupInfo?.Id;
        var senderId = message.Sender!.Id;
        lock (Handlers)
        {
            if (Handlers.Count == 0) return MiraiPluginTaskState.NoResponse;

            (long?, long?) key = (groupId, senderId);

            if (!Handlers.ContainsKey(key)) key = (groupId, null);
            if (!Handlers.ContainsKey(key)) return MiraiPluginTaskState.NoResponse;

            var handler = Handlers[key];

            switch (handler(sender, message).Result)
            {
                // 完成了，删除 handler
                case MiraiPluginTaskState.CompletedTask:
                    Handlers.Remove(key);
                    return MiraiPluginTaskState.CompletedTask;
                // 处理了一部分，但没有完成，不删除，但是终止 event 传播
                case MiraiPluginTaskState.ToBeContinued:
                    return MiraiPluginTaskState.CompletedTask;
                // handler 没处理，交给其它插件处理
                case MiraiPluginTaskState.NoResponse:
                    return MiraiPluginTaskState.NoResponse;
                // 插件自闭了，请求删除自己
                case MiraiPluginTaskState.Canceled:
                    Handlers.Remove(key);
                    return MiraiPluginTaskState.NoResponse;
                // 错误的状态，删除这个异常的 handler（虽然不太可能发生）
                default:
                    Handlers.Remove((groupId, senderId));
                    return MiraiPluginTaskState.NoResponse;
            }
        }
    }
}