namespace Marisa.Plugin.Shared.Versus;

/// <summary>n/b 子命令边界；歌曲和范围解析由各游戏提供。</summary>
public static class VersusCommands
{
    public static bool BatchTrigger(Message message, Func<string, bool> validScope)
    {
        var command = message.Command.Trim().ToString();
        return command.Length > 0 &&
               (command.Length == 1 || char.IsWhiteSpace(command[1]) || validScope(command[1..]));
    }

    public static bool RandomTrigger(Message message)
    {
        var command = message.Command.Trim().Span;
        return command.Length > 0 &&
               (command.Length == 1 || char.IsWhiteSpace(command[1]) || char.IsAsciiDigit(command[1]));
    }
}
