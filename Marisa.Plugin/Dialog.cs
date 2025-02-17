// ReSharper disable UnusedMember.Local
namespace Marisa.Plugin;

[MarisaPluginNoDoc]
[MarisaPlugin(PluginPriority.Dialog)]
[MarisaPluginCommand(MessageType.GroupMessage | MessageType.FriendMessage)]
public class Dialog : MarisaPluginBase
{
    // map (group id, user id) to handler
    private static readonly Dictionary<(long?, long?), Queue<Shared.Dialog.Dialog.MessageHandler>> Handlers = new();

    public static bool AddHandler(long? groupId, long? senderId, Shared.Dialog.Dialog.MessageHandler handler)
    {
        lock (Handlers)
        {
            if (!Handlers.ContainsKey((groupId, senderId)))
            {
                Handlers.Add((groupId, senderId), new Queue<Shared.Dialog.Dialog.MessageHandler>());
            }
            Handlers[(groupId, senderId)].Enqueue(handler);

            return true;
        }
    }

    [MarisaPluginCommand]
    private static MarisaPluginTaskState MessageHandler(Message message)
    {
        var groupId  = message.GroupInfo?.Id;
        var senderId = message.Sender.Id;

        lock (Handlers)
        {
            if (Handlers.Count == 0) return MarisaPluginTaskState.NoResponse;

            (long?, long?) key = (groupId, senderId);

            // 是否有 针对某个人的dialog
            if (!Handlers.ContainsKey(key)) key = (groupId, null);
            // 是否有 针对整个群组的dialog
            if (!Handlers.TryGetValue(key, out var value)) return MarisaPluginTaskState.NoResponse;
            if (value.Count == 0) return MarisaPluginTaskState.NoResponse;

            try
            {
                var front = value.Peek();
                switch (front(message).Result)
                {
                    // 完成了，删除 handler
                    case MarisaPluginTaskState.CompletedTask:
                        value.Dequeue();
                        return MarisaPluginTaskState.CompletedTask;
                    // 处理了一部分，但没有完成，不删除，但是终止 event 传播
                    case MarisaPluginTaskState.ToBeContinued:
                        return MarisaPluginTaskState.CompletedTask;
                    // handler 没处理，交给其它插件处理
                    case MarisaPluginTaskState.NoResponse:
                        value.Dequeue();
                        var rep = MessageHandler(message);
                        value.Enqueue(front);
                        return rep;
                    // 插件自闭了，请求删除自己
                    case MarisaPluginTaskState.Canceled:
                    // 错误的状态，删除这个异常的 handler（虽然不太可能发生）
                    default:
                        value.Dequeue();
                        return MessageHandler(message);
                }
            }
            catch
            {
                value.Dequeue();
                throw;
            }
        }
    }
}