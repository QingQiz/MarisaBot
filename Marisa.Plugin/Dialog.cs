namespace Marisa.Plugin;

using TKey = (long?, long?);

[MarisaPluginNoDoc]
[MarisaPlugin(PluginPriority.Dialog)]
[MarisaPluginCommand(MessageType.GroupMessage | MessageType.FriendMessage)]
public class Dialog : MarisaPluginBase
{
    // map (group id, user id) to handler
    private static readonly Dictionary<TKey, Shared.Dialog.Dialog.MessageHandler> Handlers = new();

    public static bool TryAddHandler(long? groupId, long? senderId, Shared.Dialog.Dialog.MessageHandler handler)
    {
        lock (Handlers)
        {
            return Handlers.TryAdd((groupId, senderId), handler);
        }
    }

    public static async Task AddHandlerAsync(long? groupId, long? senderId, Shared.Dialog.Dialog.MessageHandler handler)
    {
        var key = (groupId, senderId);
        while (true)
        {
            lock (Handlers)
            {
                if (Handlers.TryAdd(key, handler))
                {
                    break;
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(0.1));
        }
    }

    [MarisaPluginCommand]
    private static MarisaPluginTaskState MessageHandler(Message message)
    {
        var groupId  = message.GroupInfo?.Id;
        var senderId = message.Sender.Id;

        lock (Handlers)
        {
            if (Handlers.Count == 0)
                return MarisaPluginTaskState.NoResponse;
        }

        TKey key = (groupId, senderId);

        Shared.Dialog.Dialog.MessageHandler? dialogHandler;
        lock (Handlers)
        {
            // 是否有 针对某个人的dialog
            if (!Handlers.ContainsKey(key)) key = (groupId, null);
            // 是否有 针对整个群组的dialog
            if (!Handlers.TryGetValue(key, out dialogHandler)) return MarisaPluginTaskState.NoResponse;
        }

        MarisaPluginTaskState dialogRes;
        try
        {
            dialogRes = dialogHandler(message).Result;
        }
        catch
        {
            RemoveKeyFromHandler(key);
            throw;
        }

        switch (dialogRes)
        {
            // 完成了，删除 handler
            case MarisaPluginTaskState.CompletedTask:
                RemoveKeyFromHandler(key);
                return MarisaPluginTaskState.CompletedTask;
            // 处理了一部分，但没有完成，不删除，但是终止 event 传播
            case MarisaPluginTaskState.ToBeContinued:
                return MarisaPluginTaskState.CompletedTask;
            // handler 没处理，交给其它插件处理
            case MarisaPluginTaskState.NoResponse:
                RemoveKeyFromHandler(key);
                var rep = MessageHandler(message);
                lock (Handlers)
                {
                    Handlers.Add(key, dialogHandler);
                }
                return rep;
            // 插件自闭了，请求删除自己
            case MarisaPluginTaskState.Canceled:
            // 错误的状态，删除这个异常的 handler（虽然不太可能发生）
            default:
                RemoveKeyFromHandler(key);
                // ReSharper disable once TailRecursiveCall
                return MessageHandler(message);
        }
    }

    private static void RemoveKeyFromHandler(TKey key)
    {
        lock (Handlers)
        {
            Handlers.Remove(key);
        }
    }
}