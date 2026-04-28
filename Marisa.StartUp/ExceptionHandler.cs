using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Marisa.Configuration;
using NLog;

namespace Marisa.StartUp;

public class ExceptionHandler: IExceptionHandler
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var relatedMessage = $"{httpContext.Request.Method} {httpContext.Request.Path}{httpContext.Request.QueryString}";
        var dumpPath = ExceptionDump.Save(exception, relatedMessage, typeof(ExceptionHandler).FullName);
        Logger.Error("Unhandled exception: " + exception);
        if (dumpPath is not null)
        {
            Logger.Error("Exception dump saved to {0}", dumpPath);
        }
        return new ValueTask<bool>(false);
    }
}
