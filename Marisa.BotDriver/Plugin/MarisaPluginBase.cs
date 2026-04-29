using System.Reflection;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin.Attributes;
using Marisa.Configuration;
using NLog;

namespace Marisa.BotDriver.Plugin;

[MarisaPlugin]
public class MarisaPluginBase
{
    public virtual Task BackgroundService(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual Task ExceptionHandler(Exception exception, Message message)
    {
        var log = LogManager.GetCurrentClassLogger();
        var dumpPath = ExceptionDump.Save(exception, message.ToString(), GetType().FullName);
        
        log.Error($"{exception}\nCaused by message: {message}");
        if (dumpPath is not null)
        {
            log.Error("Exception dump saved to {0}", dumpPath);
        }

        static Exception Unwrap(Exception exception)
        {
            while (true)
            {
                if (exception is TargetInvocationException { InnerException: not null } targetInvocationException)
                {
                    exception = targetInvocationException.InnerException!;
                    continue;
                }

                if (exception is AggregateException { InnerExceptions.Count: 1 } aggregateException)
                {
                    exception = aggregateException.InnerExceptions[0]!;
                    continue;
                }

                return exception;
            }
        }

        var currentException = Unwrap(exception);

        if (currentException is MissingConfigurationException missingConfigurationException)
        {
            message.Reply(missingConfigurationException.UserMessage);
            return Task.CompletedTask;
        }

        message.Send(new MessageDataText("出现异常，已上报开发者"));

        return Task.CompletedTask;
    }
}
