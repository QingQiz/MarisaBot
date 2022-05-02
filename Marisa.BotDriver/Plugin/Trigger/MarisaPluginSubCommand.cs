namespace Marisa.BotDriver.Plugin.Trigger;

/// <summary>
/// 如果subcommand触发失败，会fallback到父命令并执行
/// 如果subcommand触发成功，则不执行父命令
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MarisaPluginSubCommand : Attribute
{
    public readonly string Name;

    public MarisaPluginSubCommand(string methodName)
    {
        Name = methodName;
    }
}