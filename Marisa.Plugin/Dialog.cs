namespace Marisa.Plugin;

[MarisaPluginNoDoc]
[MarisaPlugin(PluginPriority.Dialog)]
[MarisaPluginCommand(MessageType.GroupMessage | MessageType.FriendMessage)]
public class Dialog : MarisaPluginBase
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

    [MarisaPluginCommand]
    private static MarisaPluginTaskState MessageHandler(Message message)
    {
        var groupId  = message.GroupInfo?.Id;
        var senderId = message.Sender!.Id;
        lock (Handlers)
        {
            if (Handlers.Count == 0) return MarisaPluginTaskState.NoResponse;

            (long?, long?) key = (groupId, senderId);

            if (!Handlers.ContainsKey(key)) key = (groupId, null);
            if (!Handlers.ContainsKey(key)) return MarisaPluginTaskState.NoResponse;

            var handler = Handlers[key];

            switch (handler(message).Result)
            {
                // 完成了，删除 handler
                case MarisaPluginTaskState.CompletedTask:
                    Handlers.Remove(key);
                    return MarisaPluginTaskState.CompletedTask;
                // 处理了一部分，但没有完成，不删除，但是终止 event 传播
                case MarisaPluginTaskState.ToBeContinued:
                    return MarisaPluginTaskState.CompletedTask;
                // handler 没处理，交给其它插件处理
                case MarisaPluginTaskState.NoResponse:
                    Handlers.Remove(key);
                    var rep = MessageHandler(message);
                    Handlers.Add(key, handler);
                    return rep;
                // 插件自闭了，请求删除自己
                case MarisaPluginTaskState.Canceled:
                    Handlers.Remove(key);
                    return MessageHandler(message);
                // 错误的状态，删除这个异常的 handler（虽然不太可能发生）
                default:
                    Handlers.Remove((groupId, senderId));
                    return MessageHandler(message);
            }
        }
    }
}