using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Marisa.StartUp;

public static class Configuration
{
    public static void ConfigLogger(this IServiceCollection c)
    {
        // Step 1. Create configuration object 
        var config = new LoggingConfiguration();

        // Step 2. Create targets and set their properties 
        var consoleTarget = new ColoredConsoleTarget("targetConsole")
        {
            Layout = @"[${date:format=HH\:mm\:ss}][${level:uppercase=true}] ${message}"
        };
        var fileTarget = new FileTarget("targetFile")
        {
            FileName = "${basedir}/logs/app_${shortdate}.log",
            Layout   = "${longdate} ${uppercase:${level}} ${message}"
        };

        // Step 3. Add targets to the configuration
        config.AddTarget(consoleTarget);
        config.AddTarget(fileTarget);

        // Step 4. Define rules
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget));
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Warn, consoleTarget));
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Warn, consoleTarget));
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Warn, fileTarget));
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Error, fileTarget));

        // Step 5. Activate the configuration
        LogManager.Configuration = config;

        c.AddScoped<ILogger>(_ => LogManager.GetCurrentClassLogger());
    }
}