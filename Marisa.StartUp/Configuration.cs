using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.DependencyInjection;

namespace Marisa.StartUp;

public static class Configuration
{
    public static void ConfigLogger(this IServiceCollection c)
    {
        var hierarchy = (Hierarchy)LogManager.GetRepository();
        // var hierarchy = new Hierarchy();

        var patternLayout = new PatternLayout
        {
            ConversionPattern = "[%date][%level] %message%newline"
        };

        patternLayout.ActivateOptions();

        var coloredConsoleAppender = new ColoredConsoleAppender();

        coloredConsoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
            BackColor = ColoredConsoleAppender.Colors.Red,
            ForeColor = ColoredConsoleAppender.Colors.White,
            Level     = Level.Error
        });

        coloredConsoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
            ForeColor = ColoredConsoleAppender.Colors.Yellow,
            Level     = Level.Warn
        });

        coloredConsoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
            ForeColor = ColoredConsoleAppender.Colors.Red,
            Level     = Level.Fatal
        });

        coloredConsoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
            ForeColor = ColoredConsoleAppender.Colors.Green,
            Level     = Level.Debug
        });

        coloredConsoleAppender.Layout = patternLayout;

        var rollingFileAppender = new RollingFileAppender
        {
            File         = "Log.log",
            AppendToFile = true,
            RollingStyle = RollingFileAppender.RollingMode.Date,
            DatePattern  = "yyyyMMdd",
            Layout       = patternLayout
        };
        
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        coloredConsoleAppender.ActivateOptions();
        rollingFileAppender.ActivateOptions();

        hierarchy.Root.AddAppender(coloredConsoleAppender);
        hierarchy.Root.AddAppender(rollingFileAppender);
        hierarchy.Root.Level = Level.Info;
        hierarchy.Configured = true;

        c.AddScoped(_ => LogManager.GetLogger("LOG"));
    }
}