using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.StartUp;

public class Configuration
{
    private readonly ServiceCollection _serviceCollection;
    private readonly string[] _args;

    public Configuration(string[] args)
    {
        _serviceCollection = new ServiceCollection();
        _args              = args;
    }

    private void ConfigInjection()
    {
        _serviceCollection
            .AddScoped(p => p)
            .AddScoped<ServiceCollection>()
            .AddScoped<DictionaryProvider>()
            .AddScoped<MiraiHttpSession>();

        var plugins =
            Assembly.LoadFile(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "QQBOT.Plugin.dll")).GetTypes()
                .Where(t => t.GetCustomAttribute<MiraiPlugin>(true) is not null)
                .Where(t => t.GetCustomAttribute<MiraiPluginDisabled>(false) is null)
                .OrderByDescending(t => t.GetCustomAttribute<MiraiPlugin>()!.Priority);

        foreach (var plugin in plugins)
        {
            Console.WriteLine($"Enabled plugin: `{plugin}`");
            _serviceCollection.AddScoped(typeof(MiraiPluginBase), plugin);
        }
    }

    private void ConfigDictionary(IServiceProvider provider)
    {
        provider.GetService<DictionaryProvider>()!
            .Add("QQ", long.Parse(_args[1]))
            .Add("ServerAddress", _args[0])
            .Add("AuthKey", _args[2]);
    }
    

    public IServiceProvider Config()
    {
        // 配置依赖注入项
        ConfigInjection();

        var provider = _serviceCollection.BuildServiceProvider();

        // 注入命令行参数
        ConfigDictionary(provider);

        return provider;
    }
}