using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin.Attributes;

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
        if ((exception.InnerException ?? exception) is AggregateException {InnerExceptions.Count: 1 } aggregateException)
        {
            return ExceptionHandler(aggregateException.InnerExceptions[0], message);
        }

        Console.WriteLine(exception.ToString());

        message.Send(new MessageDataText(exception.InnerException?.ToString() ?? exception.ToString()));
        return Task.CompletedTask;
    }   
}