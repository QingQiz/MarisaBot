using System.Reflection;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Attributes;
using Marisa.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Polly;
using Polly.Timeout;

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
    protected readonly MessageDispatcher MessageDispatcher = new(pluginsAll, serviceProvider, dict);
    protected readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 配置依赖注入
    /// </summary>
    /// <param name="types">一堆插件的类型</param>
    /// <returns>ServiceCollection</returns>
    protected static IServiceCollection Config(Type[] types)
    {
        var sc = new ServiceCollection()
            .AddScoped(p => p)
            .AddScoped(p => (ServiceProvider)p)
            .AddScoped<DictionaryProvider>()
            .AddScoped<MessageQueueProvider>()
            .AddScoped<MessageSenderProvider>()
            // db context
            .AddScoped(_ => new BotDbContext());

        var plugins = types
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
        while (await MessageQueueProvider.RecvQueue.Reader.WaitToReadAsync())
        {
            var message = await MessageQueueProvider.RecvQueue.Reader.ReadAsync();
            _ = ProcMessageStep(message);
        }

        Logger.Fatal("Message processing task exited unexpectedly");
    }

    protected async Task ProcMessageStep(Message message)
    {
        var res = await Policy.TimeoutAsync(TimeSpan.FromMinutes(10), TimeoutStrategy.Pessimistic).ExecuteAndCaptureAsync(async () =>
        {
            Logger.Info("{0}", message.ToString());
            var toInvoke = MessageDispatcher.Dispatch(message);

            foreach (var (plugin, method, m2) in toInvoke)
            {
                var state = await MessageDispatcher.Invoke(plugin, method, m2);

                if (state == MarisaPluginTaskState.CompletedTask) break;
            }
        });

        if (res.Outcome != OutcomeType.Failure) return;

        message.Reply("Cancelled due to timeout (10min)");
        Logger.Error("Handler timed out. Caused by message: {0}", message);
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