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

        var sourcePlugin = DialogManager.GetSourcePlugin(key);

        MarisaPluginTaskState dialogRes;
        try
        {
            dialogRes = await dialogHandler!(message);
        }
        catch (Exception e)
        {
            DialogManager.RemoveDialog(key);
            if (sourcePlugin != null)
            {
                await sourcePlugin.ExceptionHandler(e, message);
                return MarisaPluginTaskState.NoResponse;
            }
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

                // If another plugin claimed the same dialog key while we let other plugins run,
                // keep the newer dialog instead of waiting forever to restore the old one.
                DialogManager.TryRestoreDialog(key, dialogHandler, sourcePlugin);
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
