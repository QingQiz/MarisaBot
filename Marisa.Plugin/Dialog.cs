using Marisa.Plugin.Shared.Dialog;

namespace Marisa.Plugin;

using TKey = (long?, long?);

[MarisaPluginNoDoc]
[MarisaPlugin(PluginPriority.Dialog)]
[MarisaPluginCommand(MessageType.GroupMessage | MessageType.FriendMessage)]
public class Dialog : MarisaPluginBase
{
    [MarisaPluginCommand]
    private static async Task<MarisaPluginTaskState> MessageHandler(Message message)
    {
        var  groupId  = message.GroupInfo?.Id;
        var  senderId = message.Sender.Id;
        TKey key      = (groupId, senderId);

        // 是否有 针对某个人的dialog
        if (!DialogManager.ContainsDialog(key)) key = (groupId, null);
        // 是否有 针对整个群组的dialog
        if (!DialogManager.TryGetDialog(key, out var dialogHandler)) return MarisaPluginTaskState.NoResponse;

        MarisaPluginTaskState dialogRes;
        try
        {
            dialogRes = dialogHandler!(message).Result;
        }
        catch
        {
            DialogManager.RemoveDialog(key);
            throw;
        }

        switch (dialogRes)
        {
            // 完成了，删除 handler
            case MarisaPluginTaskState.CompletedTask:
                DialogManager.RemoveDialog(key);
                return MarisaPluginTaskState.CompletedTask;
            // 处理了一部分，但没有完成，不删除，但是终止 event 传播
            case MarisaPluginTaskState.ToBeContinued:
                return MarisaPluginTaskState.CompletedTask;
            // handler 没处理，交给其它插件处理
            case MarisaPluginTaskState.NoResponse:
                DialogManager.RemoveDialog(key);
                var rep = await MessageHandler(message);
                await DialogManager.AddDialogAsync(key, dialogHandler);
                return rep;
            // 插件自闭了，请求删除自己
            case MarisaPluginTaskState.Canceled:
            // 错误的状态，删除这个异常的 handler（虽然不太可能发生）
            default:
                DialogManager.RemoveDialog(key);
                // ReSharper disable once TailRecursiveCall
                return await MessageHandler(message);
        }
    }
}