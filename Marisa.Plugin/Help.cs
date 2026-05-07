using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin;

[MarisaPluginDoc("给出该文档，可用`help 插件名`查看插件详细")]
[MarisaPluginCommand(true, "help", "帮助")]
public class Help : MarisaPluginBase
{
    [MarisaPluginCommand]
    private async Task<MarisaPluginTaskState> Handler(Message message)
    {
        var b64 = await WebApi.RenderUrl("/help");

        message.Reply(MessageDataImage.FromBase64(b64));

        return MarisaPluginTaskState.CompletedTask;
    }
}