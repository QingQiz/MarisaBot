using System.Reflection;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Marisa.Backend.Mirai;
using Marisa.BotDriver.DI;
using Microsoft.Extensions.DependencyInjection;

namespace Marisa.StartUp;

public class Configuration
{
    private readonly IServiceCollection _serviceCollection;
    private readonly string[] _args;

    public Configuration(string[] args)
    {
        _serviceCollection =
            MiraiBackend.Config(
                Assembly.LoadFile(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Marisa.Plugin.dll")));
        _args = args;
    }

    private void ConfigCommandLineArguments(IServiceProvider provider)
    {
        provider.GetService<DictionaryProvider>()!
            .Add("QQ", long.Parse(_args[1]))
            .Add("ServerAddress", _args[0])
            .Add("AuthKey", _args[2]);
    }

    private void ConfigLogger()
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

        _serviceCollection.AddScoped(_ => LogManager.GetLogger(GetType()));
    }

    public IServiceProvider Config()
    {
        ConfigLogger();

        var provider = _serviceCollection.BuildServiceProvider();

        // 注入命令行参数
        ConfigCommandLineArguments(provider);

        return provider;
    }
}