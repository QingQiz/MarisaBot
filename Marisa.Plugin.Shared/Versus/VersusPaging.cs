using Marisa.Plugin.Shared.Dialog;

namespace Marisa.Plugin.Shared.Versus;

public static class VersusPaging
{
    public static async Task ReplyPages(MarisaPluginBase source, Message message, int pageCount, Func<int, Task<string>> render)
    {
        var firstPage = await render(1);
        message.Reply(MessageDataImage.FromBase64(firstPage));
        if (pageCount == 1) return;

        var key = (message.GroupInfo?.Id, message.Sender.Id);
        if (!DialogManager.TryAddDialog(key, HandlePage, source)) return;

        async Task<MarisaPluginTaskState> HandlePage(Message next)
        {
            if (!next.IsPlainText()) return MarisaPluginTaskState.Canceled;

            var command = next.Command.Trim().ToString();
            if (command.Length < 2 || command[0] is not ('p' or 'P') ||
                !int.TryParse(command[1..], out var page) || page < 1 || page > pageCount)
            {
                return MarisaPluginTaskState.Canceled;
            }

            var image = page == 1 ? firstPage : await render(page);
            next.Reply(MessageDataImage.FromBase64(image));
            return MarisaPluginTaskState.ToBeContinued;
        }
    }
}
