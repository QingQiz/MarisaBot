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

        var availablePlugins = _plugins.Where(p =>
            p.GetType().GetCustomAttributes<MiraiPluginTrigger>().Any() ||
            p.GetType().GetCustomAttributes<MiraiPluginCommand>().Any()).ToList();

        var availableMethods = availablePlugins
            .Select(p => (p, p.GetType()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(m =>
                    m.GetCustomAttributes<MiraiPluginTrigger>().Any() ||
                    m.GetCustomAttributes<MiraiPluginCommand>().Any())))
            .ToDictionary(x => x.Item1, x => x.Item2.ToList());

        Task<MiraiPluginTaskState> TrigPlugin(MiraiPluginBase plugin, Message message)
        {
            foreach (var method in availableMethods[plugin].Where(m => CheckMember(m, message)))
            {
                var parameters = new List<dynamic>();

                foreach (var param in method.GetParameters())
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
                    if (method.ReturnType == typeof(Task<MiraiPluginTaskState>))
                    {
                        return (Task<MiraiPluginTaskState>)method.Invoke(plugin, parameters.ToArray())!;
                    }

                    if (method.ReturnType == typeof(MiraiPluginTaskState))
                    {
                        return Task.FromResult((MiraiPluginTaskState)method.Invoke(plugin, parameters.ToArray())!);
                    }

                    throw new Exception("插件方法返回类型无效");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());

                    var target    = message.Location;
                    var exception = e.InnerException?.ToString() ?? e.ToString();

                    _messageQueue.SendQueue.Post((MessageChain.FromPlainText(exception), message.Type, target, null));
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
                foreach (var plugin in availablePlugins.Where(p => CheckMember(p.GetType(), message)))
                {
                    var state = await TrigPlugin(plugin, message);

                    if (state == MiraiPluginTaskState.CompletedTask) break;
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