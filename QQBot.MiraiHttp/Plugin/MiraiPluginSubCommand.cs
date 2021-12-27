namespace QQBot.MiraiHttp.Plugin;

/// <summary>
/// 如果subcommand触发失败，会fallback到父命令并执行
/// 如果subcommand触发成功，则不执行父命令
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MiraiPluginSubCommand: Attribute
{
    public readonly string Name;

    public MiraiPluginSubCommand(string methodName)
    {
        Name = methodName;
    }
}