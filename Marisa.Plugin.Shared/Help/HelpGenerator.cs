using System.Reflection;

namespace Marisa.Plugin.Shared.Help;

public static class HelpGenerator
{
    private static IEnumerable<T> Filter<T>(IEnumerable<T> list) where T : MemberInfo
    {
        return list
            .Where(m =>
                // 把不允许给出文档的方法去掉
                !m.GetCustomAttributes<MarisaPluginNoDocAttribute>().Any() &&
                // 把禁用的方法去掉
                !m.GetCustomAttributes<MarisaPluginDisabledAttribute>().Any() &&
                // 有触发命令，或者没有触发命令但有文档的
                (
                    m.GetCustomAttributes<MarisaPluginCommand>().Any() ||
                    m.GetCustomAttributes<MarisaPluginDocAttribute>().Any()
                )
            );
    }

    public static List<HelpDoc> GetHelp(IEnumerable<MarisaPluginBase> plugins)
    {
        return Filter(plugins.Select(p => p.GetType())).Select(GetHelp).ToList();
    }

    public static HelpDoc GetHelp(Type plugin)
    {
        var doc = plugin.GetCustomAttribute<MarisaPluginDocAttribute>();
        var commands = plugin.GetCustomAttributes<MarisaPluginCommand>()
            .Select(c => c.Commands)
            .SelectMany(c => c)
            .Where(c => !c.IsWhiteSpace())
            .ToList();

        var availableHandler = Filter(plugin.GetAllMethods()).ToList();

        // 可用的处理器，要求不是子命令
        var handlers = availableHandler
            .Where(h => h.GetCustomAttribute<MarisaPluginSubCommand>() == null);

        return new HelpDoc(doc?.Doc ?? "", commands)
        {
            SubHelp = handlers
                .Select(h => GetHelp(h, availableHandler))
                .Where(d => d != null)
                .Select(d => d!).ToList()
        };
    }

    // 这里禁用提示，因为是递归，会发生多次遍历
    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private static HelpDoc? GetHelp(MemberInfo method, List<MethodInfo> methods)
    {
        var doc = method.GetCustomAttribute<MarisaPluginDocAttribute>();
        var commands = method.GetCustomAttributes<MarisaPluginCommand>()
            .Select(c => c.Commands)
            .SelectMany(c => c)
            .Where(c => !c.IsWhiteSpace())
            .ToList();

        // 没有文档也没有命令，那就不显示
        if (doc == null && commands.Count == 0)
        {
            return null;
        }

        var subcommandsDoc = methods
            .Where(m =>
            {
                var sub = m.GetCustomAttribute<MarisaPluginSubCommand>();
                return sub != null && sub.Name == method.Name;
            })
            .Select(sm => GetHelp(sm, methods))
            .Where(d => d != null)
            .Select(d => d!)
            .ToList();

        return new HelpDoc(doc?.Doc ?? "", commands)
        {
            SubHelp = subcommandsDoc
        };
    }
}