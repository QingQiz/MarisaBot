﻿using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.MiraiHttp;
    
public partial class MiraiHttpSession
{
    public static IServiceCollection Config(Assembly pluginAssembly)
    {
        var sc = new ServiceCollection()
            .AddScoped(p => p)
            .AddScoped(p => (ServiceProvider)p)
            .AddScoped<DictionaryProvider>()
            .AddScoped<MessageQueueProvider>()
            .AddScoped<MiraiHttpSession>();

        var plugins = pluginAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<MiraiPlugin>(true) is not null)
            .Where(t => t.GetCustomAttribute<MiraiPluginDisabled>(false) is null)
            .OrderByDescending(t => t.GetCustomAttribute<MiraiPlugin>()!.Priority);

        foreach (var plugin in plugins)
        {
            Console.WriteLine($"Enabled plugin: `{plugin}`");
            sc.AddScoped(typeof(MiraiPluginBase), plugin);
        }

        return sc;
    }
}