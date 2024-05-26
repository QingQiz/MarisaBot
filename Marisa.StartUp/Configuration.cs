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

        var debug = new FileTarget("DebugTarget")
        {
            FileName = "${basedir}/logs/debug/${shortdate}.log",
            Layout   = "[${longdate}][${level:uppercase=true}] ${message}\n\n"
        };

        var error = new FileTarget("ErrorTarget")
        {
            FileName = "${basedir}/logs/error/${shortdate}.log",
            Layout   = "[${longdate}][${level:uppercase=true}] ${message}\n\n"
        };

        var warn = new FileTarget("WarnTarget")
        {
            FileName = "${basedir}/logs/warn/${shortdate}.log",
            Layout   = "[${longdate}][${level:uppercase=true}] ${message}\n\n"
        };

        // Step 3. Add targets to the configuration
        config.AddTarget(consoleTarget);
        config.AddTarget(debug);
        config.AddTarget(error);
        config.AddTarget(warn);

        // Step 4. Define rules
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, consoleTarget));

        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, LogLevel.Debug,  debug));
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Warn, LogLevel.Warn,  warn));
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Error, LogLevel.Error,  error));

        // Step 5. Activate the configuration
        LogManager.Configuration = config;

        c.AddScoped<ILogger>(_ => LogManager.GetCurrentClassLogger());
    }
}