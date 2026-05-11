using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin;

[MarisaPluginDoc("翻译文本。", "<文本>")]
[MarisaPluginCommand("翻译", "translate", "trans")]
public class Translate : MarisaPluginBase
{
    [MarisaPluginCommand]
    private async Task<MarisaPluginTaskState> Handler(Message message)
    {
        var text = message.Command.ToString().Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            message.Reply("请提供要翻译的文本");
            return MarisaPluginTaskState.CompletedTask;
        }

        var translated = await OpenAiClient.Default.ChatAsync(
            "You are a professional translator. Translate the given text to Chinese. " +
            "Output ONLY the translated text, nothing else.",
            text
        );

        message.Reply(translated);
        return MarisaPluginTaskState.CompletedTask;
    }
}
