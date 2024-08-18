using Marisa.Plugin.Shared.Help;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.Shared.Interface;

public interface IMarisaPluginWithHelp
{
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("help")]
    MarisaPluginTaskState Help(Message message)
    {
        var doc = HelpGenerator.GetHelp(GetType());
        message.Reply(MessageDataImage.FromBase64(doc.GetImage().ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }
}