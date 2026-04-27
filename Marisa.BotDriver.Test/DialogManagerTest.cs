using Marisa.Plugin.Shared.Dialog;
using NUnit.Framework;

namespace Marisa.BotDriver.Test;

using TKey = (long? GroupId, long? SenderId);

public class DialogManagerTest
{
    [Test]
    public void TryRestoreDialog_Should_Not_Override_Newer_Handler()
    {
        TKey key = (123, 456);
        Dialog.MessageHandler handler1 = _ => Task.FromResult(Marisa.BotDriver.Plugin.MarisaPluginTaskState.NoResponse);
        Dialog.MessageHandler handler2 = _ => Task.FromResult(Marisa.BotDriver.Plugin.MarisaPluginTaskState.CompletedTask);

        DialogManager.RemoveDialog(key);

        Assert.That(DialogManager.TryAddDialog(key, handler1), Is.True);
        DialogManager.RemoveDialog(key);

        Assert.That(DialogManager.TryAddDialog(key, handler2), Is.True);
        Assert.That(DialogManager.TryRestoreDialog(key, handler1), Is.False);
        Assert.That(DialogManager.TryGetDialog(key, out var restoredHandler), Is.True);
        Assert.That(restoredHandler, Is.SameAs(handler2));

        DialogManager.RemoveDialog(key);
    }
}
