using Flurl.Http;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin;

[MarisaPluginDoc("翻译文本。", "<文本>")]
[MarisaPluginCommand("翻译", "translate", "trans")]
public class Translate : MarisaPluginBase, IHandleCommonException
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
            "You are a professional Chinese translator and native speaker of Chinese. Translate the user's text into natural, idiomatic Chinese that reads as if originally written by a native speaker. Match the original tone and style. Avoid literal, stiff, or AI-sounding phrasing—restructure sentences when needed for fluency.",
            $"{text}",
            thinking: ThinkingMode.Disabled
        );

        message.Reply(translated);
        return MarisaPluginTaskState.CompletedTask;
    }

    public override Task ExceptionHandler(Exception exception, Message message)
    {
        if (CommonExceptionHandler.TryHandleCommonException(exception, message))
        {
            return Task.CompletedTask;
        }

        switch (CommonExceptionHandler.UnwrapCommonException(exception))
        {
            case FlurlHttpTimeoutException:
                message.Reply("超时");
                break;
            case FlurlHttpException e:
                message.Reply(e.Message);
                break;
            default:
                return base.ExceptionHandler(exception, message);
        }

        return Task.CompletedTask;
    }
}
