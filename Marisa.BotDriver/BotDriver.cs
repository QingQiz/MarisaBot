﻿using System.Reflection;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Attributes;
using Marisa.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Marisa.BotDriver;

public abstract class BotDriver(
    IServiceProvider serviceProvider,
    IEnumerable<MarisaPluginBase> pluginsAll,
    DictionaryProvider dict,
    MessageSenderProvider messageSenderProvider,
    MessageQueueProvider messageQueueProvider)
{
    protected readonly MessageSenderProvider MessageSenderProvider = messageSenderProvider;
    protected readonly MessageQueueProvider MessageQueueProvider = messageQueueProvider;

    /// <summary>
    /// 配置依赖注入
    /// </summary>
    /// <param name="pluginAssembly"></param>
    /// <returns>ServiceCollection</returns>
    protected static IServiceCollection Config(Assembly pluginAssembly)
    {
        var sc = new ServiceCollection()
            .AddScoped(p => p)
            .AddScoped(p => (ServiceProvider)p)
            .AddScoped<DictionaryProvider>()
            .AddScoped<MessageQueueProvider>()
            .AddScoped<MessageSenderProvider>()
            // db context
            .AddScoped(_ => new BotDbContext());

        var plugins = pluginAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<MarisaPluginAttribute>(true) is not null)
            .Where(t => t.GetCustomAttribute<MarisaPluginDisabledAttribute>(false) is null)
            .OrderByDescending(t => t.GetCustomAttribute<MarisaPluginAttribute>()!.Priority);

        var logger = LogManager.GetCurrentClassLogger();

        foreach (var plugin in plugins)
        {
            logger.Info($"Enabled plugin: `{plugin}`");
            sc.AddScoped(typeof(MarisaPluginBase), plugin);
        }

        return sc;
    }

    /// <summary>
    /// 处理消息的默认实现
    /// </summary>
    /// <exception cref="Exception"></exception>
    protected virtual async Task ProcMessage()
    {
        var dispatcher = new MessageDispatcher(pluginsAll, serviceProvider, dict);
        var logger     = LogManager.GetCurrentClassLogger();

        var tasks = new List<Task>();

        while (await MessageQueueProvider.RecvQueue.Reader.WaitToReadAsync())
        {
            var messageRecv = MessageQueueProvider.RecvQueue.Reader.ReadAllAsync();

            tasks.Add(Parallel.ForEachAsync(messageRecv, async (m1, _) =>
            {
                var toInvoke = dispatcher.Dispatch(m1);
                var log      = new List<string>();

                foreach (var (plugin, method, m2) in toInvoke)
                {
                    log.Add($"[{plugin.GetType().Name}.{method.Name}]");

                    var state = await dispatcher.Invoke(plugin, method, m2);

                    if (state == MarisaPluginTaskState.CompletedTask) break;
                }

                logger.Info("Dispatched Message to {1}: {0}", m1.ToString(), string.Join("", log));
            }));

            if (tasks.Count < 100) continue;

            await Task.WhenAll();
            tasks.Clear();
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 从服务器拉取消息并更新接收队列
    /// </summary>
    protected abstract Task RecvMessage();

    /// <summary>
    /// 从接收队列接收消息并发送到服务器
    /// </summary>
    protected abstract Task SendMessage();

    /// <summary>
    /// 登录
    /// </summary>
    /// <returns></returns>
    public virtual Task Login()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 调用bot的默认实现
    /// </summary>
    public virtual async Task Invoke()
    {
        await Task.WhenAll(
            Parallel.ForEachAsync(pluginsAll, async (p, _) => await p.BackgroundService()),
            RecvMessage(), SendMessage(), ProcMessage()
        );
    }
}