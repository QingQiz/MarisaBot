using System.Text.Json;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin.Attributes;
using NLog;

namespace Marisa.BotDriver.Plugin;

[MarisaPlugin]
public class MarisaPluginBase
{
    public virtual Task BackgroundService()
    {
        return Task.CompletedTask;
    }

    public virtual Task ExceptionHandler(Exception exception, Message message)
    {
        LogManager.GetCurrentClassLogger().Error(exception + "\nCasused by message: " + JsonSerializer.Serialize(message));

        while (true)
        {
            if ((exception.InnerException ?? exception) is AggregateException { InnerExceptions.Count: 1 } aggregateException)
            {
                exception = aggregateException.InnerExceptions[0];
                continue;
            }

            message.Send(new MessageDataText(exception.InnerException?.ToString() ?? exception.ToString()));

            return Task.CompletedTask;
        }
    }
}