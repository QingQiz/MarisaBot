using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.Shared.Interface;

public interface IMarisaPluginWithHelp
{
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("help")]
    async Task<MarisaPluginTaskState> Help(Message message)
    {
        var b64 = await WebApi.RenderUrl($"/help?plugin={GetType().Name}");

        message.Reply(MessageDataImage.FromBase64(b64));

        return MarisaPluginTaskState.CompletedTask;
    }
}