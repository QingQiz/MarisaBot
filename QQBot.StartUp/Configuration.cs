﻿using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;

namespace QQBot.StartUp;

public class Configuration
{
    private readonly IServiceCollection _serviceCollection;
    private readonly string[] _args;

    public Configuration(string[] args)
    {
        _serviceCollection =
            MiraiHttpSession.Config(
                Assembly.LoadFile(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "QQBot.Plugin.dll")));
        _args = args;
    }

    private void ConfigCommandLineArguments(IServiceProvider provider)
    {
        provider.GetService<DictionaryProvider>()!
            .Add("QQ", long.Parse(_args[1]))
            .Add("ServerAddress", _args[0])
            .Add("AuthKey", _args[2]);
    }

    public IServiceProvider Config()
    {
        var provider = _serviceCollection.BuildServiceProvider();

        // 注入命令行参数
        ConfigCommandLineArguments(provider);

        return provider;
    }
}