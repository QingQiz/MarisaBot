using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

public static class PluginUtils
{
    public static IEnumerable<Type> EnabledPlugins()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(MiraiPluginAttribute), true) is { Length: > 0 })
                .Where(t => t.GetCustomAttributes(typeof(MiraiPluginDisabledAttribute), false) is not
                    { Length: > 0 }))
            .SelectMany(t => t)
            .OrderByDescending(t =>
                ((MiraiPluginAttribute)t.GetCustomAttributes(typeof(MiraiPluginAttribute), true).First()).Priority);
    }
}