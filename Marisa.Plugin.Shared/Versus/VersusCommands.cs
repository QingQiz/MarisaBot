namespace Marisa.Plugin.Shared.Versus;

/// <summary>n/b 子命令边界与 @ 位置；歌曲和范围解析由各游戏提供。</summary>
public static class VersusCommands
{
    public static bool BatchTrigger(Message message, Func<string, bool> validScope)
    {
        var command = message.Command.Trim().ToString();
        return command.Length > 0 &&
               (command.Length == 1 || char.IsWhiteSpace(command[1]) || validScope(command[1..])) &&
               SubCommandBeforeMention(message);
    }

    public static bool RandomTrigger(Message message)
    {
        var command = message.Command.Trim().Span;
        return command.Length > 0 &&
               (command.Length == 1 || char.IsWhiteSpace(command[1]) || char.IsAsciiDigit(command[1])) &&
               SubCommandBeforeMention(message);
    }

    private static bool SubCommandBeforeMention(Message message)
    {
        var chain = message.MessageChain!;
        var beforeMention = new MessageChain(chain.Messages.TakeWhile(x => x.Type != MessageDataType.At)).Text.Trim();
        var consumed = chain.Text.Trim().Length - message.Command.TrimStart().Length;
        return beforeMention.Length > consumed;
    }
}
