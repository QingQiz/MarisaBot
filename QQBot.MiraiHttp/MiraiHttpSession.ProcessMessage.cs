using System.Reflection;
using System.Threading.Tasks.Dataflow;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.MiraiHttp;

public partial class MiraiHttpSession
{
    private async Task ProcMessage()
    {
        bool Check(Message message, IReadOnlyCollection<MiraiPluginCommand> commands, IReadOnlyCollection<MiraiPluginTrigger> triggers)
        {
            if (triggers.Any())
            {
                var resTrigger = triggers.Aggregate(false,
                    (result, trigger) => result || trigger.Trigger(message, _serviceProvider));

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
            var commands = plugin.GetCustomAttributes<MiraiPluginCommand>().ToList();
            var triggers = plugin.GetCustomAttributes<MiraiPluginTrigger>().ToList();

            return Check(message, commands, triggers);
        }

        const BindingFlags bindingFlags = BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

        var availablePlugins = _plugins.Where(p =>
            p.GetType().GetCustomAttributes<MiraiPluginTrigger>().Any() ||
            p.GetType().GetCustomAttributes<MiraiPluginCommand>().Any()).ToList();

        var availableMethods = availablePlugins
            .Select(p => (p, p.GetType()
                .GetMethods(bindingFlags)
                // 选择出含有这两个属性的方法
                .Where(m =>
                    m.GetCustomAttributes<MiraiPluginTrigger>().Any() ||
                    m.GetCustomAttributes<MiraiPluginCommand>().Any())
                // 过滤 subcommand
                .Where(m =>
                    !m.GetCustomAttributes<MiraiPluginSubCommand>().Any())))
            .ToDictionary(x => x.Item1, x => x.Item2.ToList());

        var availableSubCommands = availablePlugins
            .Select(p => (p, p.GetType()
                .GetMethods(bindingFlags)
                // 选择出含有这两个属性的方法
                .Where(m =>
                    m.GetCustomAttributes<MiraiPluginTrigger>().Any() ||
                    m.GetCustomAttributes<MiraiPluginCommand>().Any())
                // 包含 subcommand
                .Where(m =>
                    m.GetCustomAttributes<MiraiPluginSubCommand>().Any())
                .Select(m =>
                    (parentName: m.GetCustomAttribute<MiraiPluginSubCommand>()!.Name, methodInfo: m))))
            .ToDictionary(x => x.Item1, x => x.Item2.ToList());

        Task<MiraiPluginTaskState> TrigPlugin(MiraiPluginBase plugin, Message message)
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

                foreach (var param in m.GetParameters())
                {
                    if (param.ParameterType == message.GetType())
                    {
                        parameters.Add(message);
                    }
                    else
                    {
                        var p = _serviceProvider.GetService(param.ParameterType);
                        parameters.Add(p ?? _dictionaryProvider[param.Name!]);
                    }
                }

                try
                {
                    if (m.ReturnType == typeof(Task<MiraiPluginTaskState>))
                    {
                        return (Task<MiraiPluginTaskState>)m.Invoke(plugin, parameters.ToArray())!;
                    }

                    if (m.ReturnType == typeof(MiraiPluginTaskState))
                    {
                        return Task.FromResult((MiraiPluginTaskState)m.Invoke(plugin, parameters.ToArray())!);
                    }

                    throw new Exception("插件方法返回类型无效");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());

                    var target    = message.Location;
                    var exception = e.InnerException?.ToString() ?? e.ToString();

                    _messageQueue.SendQueue.Post((MessageChain.FromPlainText(exception), message.Type, target, null, null));
                }
            }

            return Task.FromResult(MiraiPluginTaskState.NoResponse);
        }

        var taskList = new List<Task>();

        while (await _messageQueue.RecvQueue.OutputAvailableAsync())
        {
            var messageRecv = _messageQueue.RecvQueue.ReceiveAllAsync();
            
            taskList.Add(Parallel.ForEachAsync(messageRecv, async (message, _) =>
            {
                var command = message.Command;

                foreach (var plugin in availablePlugins.Where(p => CheckMember(p.GetType(), message)))
                {
                    var state   = await TrigPlugin(plugin, message);

                    if (state == MiraiPluginTaskState.CompletedTask) break;

                    message.Command = command;
                }
            }));

            // ReSharper disable once InvertIf
            if (taskList.Count >= 100)
            {
                await Task.WhenAll();
                taskList.Clear();
            }
        }

        await Task.WhenAll(taskList);
    }
}