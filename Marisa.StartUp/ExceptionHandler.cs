using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using NLog;

namespace Marisa.StartUp;

public class ExceptionHandler: IExceptionHandler
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        Logger.Error("Unhandled exception: " + exception);
        return new ValueTask<bool>(false);
    }
}