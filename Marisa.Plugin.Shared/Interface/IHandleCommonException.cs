using System.Reflection;

namespace Marisa.Plugin.Shared.Interface;

public interface IHandleCommonException;

public static class CommonExceptionHandler
{
    public static Task HandleCommonExceptionOr(Exception exception, Message message, Func<Task> fallback)
    {
        return TryHandleCommonException(exception, message)
            ? Task.CompletedTask
            : fallback();
    }

    public static bool TryHandleCommonException(Exception exception, Message message)
    {
        var currentException = UnwrapCommonException(exception);

        if (currentException is not WebRenderFailedException webRenderFailedException)
        {
            return false;
        }

        message.Reply(webRenderFailedException.PublicUrl);
        return true;
    }

    public static Exception UnwrapCommonException(Exception exception)
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
}