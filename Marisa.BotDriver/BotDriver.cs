using System.Reflection;
using System.Threading.Tasks.Dataflow;
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
    public static IServiceCollection Config(Assembly pluginAssembly)
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
        bool Check(
            Message message, IReadOnlyCollection<MarisaPluginCommand> commands,
            IReadOnlyCollection<MarisaPluginTrigger> triggers)
        {
            if (triggers.Any())
            {
                var resTrigger = triggers.Aggregate(false,
                    (result, trigger) => result || trigger.Trigger(message, ServiceProvider));

                if (!resTrigger) return false;
            }

            // ReSharper disable once InvertIf
            if (commands.Any())
            {
                foreach (var command in commands.Where(command => command.Check(message)))
                {
                    message.Command = command.Trim(message);
                    return true;
                }

                return false;
            }

            return true;
        }

        bool CheckMember(MemberInfo plugin, Message message)
        {
            var commands = plugin.GetCustomAttributes<MarisaPluginCommand>().ToList();
            var triggers = plugin.GetCustomAttributes<MarisaPluginTrigger>().ToList();

            return Check(message, commands, triggers);
        }

        const BindingFlags bindingFlags = BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.Public;

        var availablePlugins = Plugins.Where(p =>
            p.GetType().GetCustomAttributes<MarisaPluginTrigger>().Any() ||
            p.GetType().GetCustomAttributes<MarisaPluginCommand>().Any()).ToList();

        var availableMethods = availablePlugins
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

        var availableSubCommands = availablePlugins
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

        async IAsyncEnumerable<MarisaPluginTaskState> TrigPlugin(MarisaPluginBase plugin, Message message)
        {
            foreach (var method in availableMethods[plugin].Where(m => CheckMember(m, message)))
            {
                var m = method;

                while (true)
                {
                    // 所有子命令
                    var subCommands = availableSubCommands[plugin]
                        .Where(n => n.parentName == method.Name)
                        .Select(n => n.methodInfo)
                        .ToList();
                    // 没有子命令则执行当前
                    if (!subCommands.Any()) break;
                    // 检查子命令
                    var sub = subCommands.FirstOrDefault(sub => CheckMember(sub, message));
                    // 没有有成功的则执行当前
                    if (sub is null)
                    {
                        break;
                    }

                    // 有成功的则继续找当前命令的子命令
                    m = sub;
                }

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
                MarisaPluginTaskState? ret = null;
                try
                {
                    if (m.ReturnType == typeof(Task<MarisaPluginTaskState>))
                    {
                        ret = await (Task<MarisaPluginTaskState>)m.Invoke(plugin, parameters.ToArray())!;
                    }
                    else if (m.ReturnType == typeof(MarisaPluginTaskState))
                    {
                        ret = (MarisaPluginTaskState)m.Invoke(plugin, parameters.ToArray())!;
                    }
                    else
                    {
                        throw new Exception("插件方法返回类型无效");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());

                    var target    = message.Location;
                    var exception = e.InnerException?.ToString() ?? e.ToString();

                    MessageSenderProvider.Send(exception, message.Type, target, null);
                }

                if (ret != null)
                {
                    // NOTE 当一个插件中有多个指令会被触发时，分为以下情况：
                    // 1. 使用 Command 同时触发了 A 和 B，此时A、B会存在相同的触发前缀，
                    //    这样的情况下需设计成子命令的形式以保证正确运行（因为A触发后消息会被修改）
                    //    若是 A 的触发前缀是空字符串，则没有影响
                    // 2. 同上，但是有 Trigger 的参与，此时没什么影响，还是应当设计成子命令的形式
                    // 3. 只有 Trigger 作用，没什么要注意的
                    yield return (MarisaPluginTaskState)ret;
                }
            }
        }

        var taskList = new List<Task>();

        while (await MessageQueueProvider.RecvQueue.OutputAvailableAsync())
        {
            var messageRecv = MessageQueueProvider.RecvQueue.ReceiveAllAsync();

            taskList.Add(Parallel.ForEachAsync(messageRecv, async (message, _) =>
            {
                var command = message.Command;

                foreach (var plugin in availablePlugins.Where(p => CheckMember(p.GetType(), message)))
                {
                    var shouldBreak = false;

                    await foreach (var state in TrigPlugin(plugin, message))
                    {
                        if (state == MarisaPluginTaskState.CompletedTask) shouldBreak = true;
                    }

                    message.Command = command;

                    if (shouldBreak) break;
                }
            }));

            if (taskList.Count < 100) continue;

            await Task.WhenAll();
            taskList.Clear();
        }

        await Task.WhenAll(taskList);
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