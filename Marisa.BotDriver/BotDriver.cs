using System.Reflection;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Attributes;
using Marisa.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Msg = Marisa.BotDriver.Entity.Message.Message;

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

        while (await MessageQueueProvider.RecvQueue.Reader.WaitToReadAsync())
        {
            var m = await MessageQueueProvider.RecvQueue.Reader.ReadAsync();

            _ = Task.Run(async () =>
            {
                var cancel = new CancellationTokenSource();

                var handlerTask = MsgProcTask(m);
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5), cancel.Token);

                var completedTask = await Task.WhenAny(handlerTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    logger.Error("Handler timed out. Caused by message: {0}", m);
                    // 无法取消，先记录日志
                }
                else
                {
                    await Task.WhenAll(cancel.CancelAsync(), handlerTask);
                }
            });
        }

        logger.Fatal("Message processing task exited unexpectedly");
        return;

        async Task MsgProcTask(Msg m)
        {
            logger.Info("{0}", m.ToString());
            var toInvoke = dispatcher.Dispatch(m);

            foreach (var (plugin, method, m2) in toInvoke)
            {
                var state = await dispatcher.Invoke(plugin, method, m2);

                if (state == MarisaPluginTaskState.CompletedTask) break;
            }
        }
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
            Task.WhenAll(pluginsAll.Select(p => p.BackgroundService())),
            RecvMessage(), SendMessage(), ProcMessage()
        );
    }
}