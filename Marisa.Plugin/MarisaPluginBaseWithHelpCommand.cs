namespace Marisa.Plugin;

[MarisaPluginDisabled]
public class MarisaPluginBaseWithHelpCommand : MarisaPluginBase
{
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("help")]
    protected MarisaPluginTaskState Help(Message message)
    {
        var doc = Plugin.Help.Help.GetHelp(GetType());
        message.Reply(MessageDataImage.FromBase64(doc.GetImage().ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }
}