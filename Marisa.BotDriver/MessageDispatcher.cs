using System.Reflection;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;

namespace Marisa.BotDriver;

public class MessageDispatcher(IEnumerable<MarisaPluginBase> pluginsAll, IServiceProvider serviceProvider, DictionaryProvider dict)
{
    private static List<MarisaPluginBase>? _plugins;
    private static Dictionary<MarisaPluginBase, List<MethodInfo>>? _commands;
    private static Dictionary<MarisaPluginBase, List<(string ParentName, MethodInfo MethodInfo)>>? _subCommands;

    private const BindingFlags BindingFlags =
        System.Reflection.BindingFlags.Default
      | System.Reflection.BindingFlags.NonPublic
      | System.Reflection.BindingFlags.Instance
      | System.Reflection.BindingFlags.Static
      | System.Reflection.BindingFlags.Public;

    /**
     * 从所有注册的插件中获取有触发器的插件
     */
    private List<MarisaPluginBase> Plugins => _plugins ??=
        pluginsAll.Where(p =>
            p.GetType().GetCustomAttributes<MarisaPluginTrigger>().Any() ||
            p.GetType().GetCustomAttributes<MarisaPluginCommand>().Any()).ToList();

    /// <summary>
    /// 获取所有 有触发器的插件 的 所有命令，<b>不</b> 包括子命令
    /// </summary>
    private Dictionary<MarisaPluginBase, List<MethodInfo>> Commands => _commands ??=
        Plugins
            .Select(p => (p, p.GetType()
                .GetMethods(BindingFlags)
                // 选择出含有这两个属性的方法
                .Where(m =>
                    m.GetCustomAttributes<MarisaPluginTrigger>().Any() ||
                    m.GetCustomAttributes<MarisaPluginCommand>().Any())
                // 过滤 subcommand
                .Where(m =>
                    !m.GetCustomAttributes<MarisaPluginSubCommand>().Any())))
            .ToDictionary(x => x.Item1, x => x.Item2.ToList());

    /// <summary>
    /// 获取所有 有触发器的插件 的 所有<b>子</b>命令
    /// </summary>
    private Dictionary<MarisaPluginBase, List<(string ParentName, MethodInfo MethodInfo)>> SubCommands => _subCommands ??=
        Plugins
            .Select(p => (p, p.GetType()
                .GetMethods(BindingFlags)
                // 选择出含有这两个属性的方法
                .Where(m =>
                    m.GetCustomAttributes<MarisaPluginTrigger>().Any() ||
                    m.GetCustomAttributes<MarisaPluginCommand>().Any())
                // 包含 subcommand
                .Where(m =>
                    m.GetCustomAttributes<MarisaPluginSubCommand>().Any())
                .Select(m =>
                    (ParentName: m.GetCustomAttribute<MarisaPluginSubCommand>()!.Name, MethodInfo: m))))
            .ToDictionary(x => x.Item1, x => x.Item2.ToList());

    /// <summary>
    /// 分发消息到各个插件
    /// </summary>
    /// <param name="message"></param>
    public IEnumerable<(MarisaPluginBase Plugin, MethodInfo Method, Message Message)> Dispatch(Message message)
    {
        var m1 = message;

        foreach (var plugin in Plugins)
        {
            if (!ShouldTrigger(plugin.GetType(), m1, out var t1)) continue;

            var m2 = m1 with { Command = t1 };

            foreach (var method in Commands[plugin])
            {
                if (!ShouldTrigger(method, m2, out var t2)) continue;

                var (which, what) = WhichMethodShouldBeTriggeredByWhat(plugin, method, message with { Command = t2 });

                yield return (plugin, which, what);
            }
        }
    }

    /// <summary>
    /// 使用Message去调用Plugin.Method，直到返回CompletedTask
    /// </summary>
    public async Task<MarisaPluginTaskState> Invoke(MarisaPluginBase plugin, MethodInfo method, Message message)
    {
        return await DependencyInjectInvoke(plugin, method, message);
    }

    /// <summary>
    /// <paramref name="message"/>是否应该被<paramref name="member"/>触发
    /// </summary>
    /// <param name="member"></param>
    /// <param name="message"></param>
    /// <param name="afterTrigger">触发后的消息剩余什么</param>
    /// <returns>是否被触发</returns>
    private bool ShouldTrigger(MemberInfo member, Message message, out ReadOnlyMemory<char> afterTrigger)
    {
        afterTrigger = message.Command;

        var c = member.GetCustomAttribute<MarisaPluginCommand>();
        var t = member.GetCustomAttribute<MarisaPluginTrigger>();

        if (c == null && t == null) return false;

        if (t != null)
        {
            var triggerResult = t.Trigger(message, serviceProvider);
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

    /// <summary>
    /// 一个插件中的哪个方法会被触发，触发这个方法的message是什么
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="handler"></param>
    /// <param name="message">触发前的message</param>
    /// <returns>
    /// 元组
    /// <list type="MethodInfo">
    /// <item>1. 哪个方法会被触发，为<paramref name="handler"/>或其子命令</item>
    /// <item>2. 触发后的message（可能只有Command不同）</item>
    /// </list>
    /// </returns>
    private (MethodInfo witch, Message what) WhichMethodShouldBeTriggeredByWhat(MarisaPluginBase plugin, MethodInfo handler, Message message)
    {
        // 获取当前handler的所有子handler
        var currentSub = SubCommands[plugin]
            .Where(n => n.ParentName == handler.Name)
            .Select(n => n.MethodInfo)
            .ToList();

        // 没有子命令则执行当前
        if (currentSub.Count == 0) return (handler, message);

        // 检查子命令能否执行
        foreach (var sub in currentSub)
        {
            if (ShouldTrigger(sub, message, out var after))
                return (sub, message with { Command = after });
        }

        // 不能就执行当前
        return (handler, message);
    }

    private async Task<MarisaPluginTaskState> DependencyInjectInvoke(MarisaPluginBase plugin, MethodInfo m, Message message)
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
                var p = serviceProvider.GetService(param.ParameterType);
                parameters.Add(p ?? dict[param.Name!]);
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
            return MarisaPluginTaskState.NoResponse;
        }
    }

}