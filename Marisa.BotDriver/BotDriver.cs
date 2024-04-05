using System.Reflection;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Attributes;
using Marisa.BotDriver.Plugin.Trigger;
using Marisa.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marisa.BotDriver;

public abstract class BotDriver
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IEnumerable<MarisaPluginBase> Plugins;
    protected readonly DictionaryProvider DictionaryProvider;
    protected readonly MessageSenderProvider MessageSenderProvider;
    protected readonly MessageQueueProvider MessageQueueProvider;

    protected BotDriver(
        IServiceProvider serviceProvider, IEnumerable<MarisaPluginBase> plugins,
        DictionaryProvider dict, MessageSenderProvider messageSenderProvider,
        MessageQueueProvider messageQueueProvider)
    {
        ServiceProvider       = serviceProvider;
        Plugins               = plugins;
        DictionaryProvider    = dict;
        MessageSenderProvider = messageSenderProvider;
        MessageQueueProvider  = messageQueueProvider;
    }

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

        foreach (var plugin in plugins)
        {
            Console.WriteLine($"Enabled plugin: `{plugin}`");
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
        const BindingFlags bindingFlags = BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static  | BindingFlags.Public;

        // get all MarisaPluginBase from plugins
        var plugins = Plugins.Where(p =>
            p.GetType().GetCustomAttributes<MarisaPluginTrigger>().Any() ||
            p.GetType().GetCustomAttributes<MarisaPluginCommand>().Any()).ToList();

        // get all Command or Trigger from MarisaPluginBase
        // filter out subcommand
        var commands = plugins
            .Select(p => (p, p.GetType()
                .GetMethods(bindingFlags)
                // 选择出含有这两个属性的方法
                .Where(m =>
                    m.GetCustomAttributes<MarisaPluginTrigger>().Any() ||
                    m.GetCustomAttributes<MarisaPluginCommand>().Any())
                // 过滤 subcommand
                .Where(m =>
                    !m.GetCustomAttributes<MarisaPluginSubCommand>().Any())))
            .ToDictionary(x => x.Item1, x => x.Item2.ToList());

        // get all Command or Trigger from MarisaPluginBase
        // filter subcommand
        var subCommands = plugins
            .Select(p => (p, p.GetType()
                .GetMethods(bindingFlags)
                // 选择出含有这两个属性的方法
                .Where(m =>
                    m.GetCustomAttributes<MarisaPluginTrigger>().Any() ||
                    m.GetCustomAttributes<MarisaPluginCommand>().Any())
                // 包含 subcommand
                .Where(m =>
                    m.GetCustomAttributes<MarisaPluginSubCommand>().Any())
                .Select(m =>
                    (parentName: m.GetCustomAttribute<MarisaPluginSubCommand>()!.Name, methodInfo: m))))
            .ToDictionary(x => x.Item1, x => x.Item2.ToList());

        var tasks = new List<Task>();

        while (await MessageQueueProvider.RecvQueue.Reader.WaitToReadAsync())
        {
            var messageRecv = MessageQueueProvider.RecvQueue.Reader.ReadAllAsync();

            tasks.Add(Parallel.ForEachAsync(messageRecv, async (message, _) =>
            {
                foreach (var plugin in plugins)
                {
                    if (!ShouldTrigger(plugin.GetType(), message, out var afterTrigger)) continue;

                    var shouldBreak = false;

                    await foreach (var state in TriggerPlugin(plugin, message with { Command = afterTrigger }))
                    {
                        if (state != MarisaPluginTaskState.CompletedTask) continue;

                        shouldBreak = true;
                    }

                    if (shouldBreak) break;
                }
            }));

            if (tasks.Count < 100) continue;

            await Task.WhenAll();
            tasks.Clear();
        }

        await Task.WhenAll(tasks);
        return;

        bool ShouldTrigger(MemberInfo member, Message message, out string afterTrigger)
        {
            afterTrigger = message.Command;

            var c = member.GetCustomAttribute<MarisaPluginCommand>();
            var t = member.GetCustomAttribute<MarisaPluginTrigger>();

            if (c == null && t == null) return false;

            if (t != null)
            {
                var triggerResult = t.Trigger(message, ServiceProvider);
                if (!triggerResult) return false;
            }

            // ReSharper disable once InvertIf
            if (c != null)
            {
                var matchResult = c.TryMatch(message, out var afterMatch);
                if (!matchResult) return false;

                afterTrigger = afterMatch;
            }

            return true;
        }

        (MethodInfo witch, Message what) WhichMethodShouldBeTriggeredByWhat(MarisaPluginBase plugin, MethodInfo handler,
            Message message)
        {
            // 获取当前handler的所有子handler
            var currentSub = subCommands[plugin]
                .Where(n => n.parentName == handler.Name)
                .Select(n => n.methodInfo)
                .ToList();

            // 没有子命令则执行当前
            if (!currentSub.Any()) return (handler, message);

            // 检查子命令能否执行
            foreach (var sub in currentSub)
            {
                if (ShouldTrigger(sub, message, out var after)) return (sub, message with { Command = after });
            }

            // 不能就执行当前
            return (handler, message);
        }

        async IAsyncEnumerable<MarisaPluginTaskState> TriggerPlugin(MarisaPluginBase plugin, Message message)
        {
            foreach (var method in commands[plugin])
            {
                if (!ShouldTrigger(method, message, out var afterTrigger)) continue;

                var (which, what) =
                    WhichMethodShouldBeTriggeredByWhat(plugin, method, message with { Command = afterTrigger });

                var ret = await DependencyInjectInvoke(plugin, which, what);

                if (ret != null)
                {
                    yield return (MarisaPluginTaskState)ret;
                }
            }
        }
    }

    private async Task<MarisaPluginTaskState?> DependencyInjectInvoke(MarisaPluginBase plugin, MethodInfo m, Message message)
    {
        var parameters = new List<dynamic>();

        // 构造参数列表
        foreach (var param in m.GetParameters())
        {
            if (param.ParameterType == message.GetType())
            {
                parameters.Add(message);
            }
            else
            {
                var p = ServiceProvider.GetService(param.ParameterType);
                parameters.Add(p ?? DictionaryProvider[param.Name!]);
            }
        }

        // 调用处理函数并处理异常
        try
        {
            if (m.ReturnType == typeof(Task<MarisaPluginTaskState>))
            {
                return await (Task<MarisaPluginTaskState>)m.Invoke(plugin, parameters.ToArray())!;
            }

            if (m.ReturnType == typeof(MarisaPluginTaskState))
            {
                return (MarisaPluginTaskState)m.Invoke(plugin, parameters.ToArray())!;
            }

            throw new Exception("插件方法返回类型无效");
        }
        catch (Exception e)
        {
            await plugin.ExceptionHandler(e, message);
            return null;
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
    /// 调用bot的默认实现
    /// </summary>
    public virtual async Task Invoke()
    {
        await Task.WhenAll(Parallel.ForEachAsync(Plugins, async (p, _) => await p.BackgroundService()),
            RecvMessage(), SendMessage(), ProcMessage());
    }
}