using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.FSharp.Osu;
using Marisa.Plugin.Shared.Osu;

namespace Marisa.Plugin.Osu;

public partial class Osu
{
    private static readonly HashSet<long> Debounce = new();

    private static void DebounceCancel(long uid)
    {
        lock (Debounce)
        {
            Debounce.Remove(uid);
        }
    }

    private static bool DebounceCheck(Message message)
    {
        lock (Debounce)
        {
            if (Debounce.Contains(message.Sender!.Id))
            {
                message.Reply(new [] {"别急", "你先别急", "急你妈", "有点急", "你急也没用"}.RandomTake());
                return true;
            }

            Debounce.Add(message.Sender!.Id);
            return false;
        }
    }

    private static void AddCommandToQueue(Message message)
    {
        message.Reply("服务暂时不可用！");
    }

    private static OsuCommandParser.OsuCommand? ParseCommand(Message message, bool withBpRank = false)
    {
        var command = OsuCommandParser.parser(message.Command)?.Value;

        if (command == null)
        {
            return null;
        }

        if (command.BpRank != null && !withBpRank)
        {
            return null;
        }

        var db = new BotDbContext();

        // 有osu id了，看看id在数据库里有没有，有的话设置一下mode
        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            // 设置了mode的话就直接返回
            if (command.Mode != null) return command;

            var u = db.OsuBinds.FirstOrDefault(o => o.OsuUserName == command.Name);

            return u == null
                ? command
                : new OsuCommandParser.OsuCommand(command.Name, command.BpRank, OsuApi.ModeList.IndexOf(u.GameMode));
        }

        // 没有osu id的话在数据库里找，找at的人或发送者
        var o = message.MessageChain?.Messages.FirstOrDefault(m => m.Type == MessageDataType.At) is MessageDataAt at
            ? db.OsuBinds.FirstOrDefault(o => o.UserId == at.Target)
            : db.OsuBinds.FirstOrDefault(o => o.UserId == message.Sender!.Id);

        // 没找到证明不知道查谁的
        if (o == null) return command;

        // 找到了就把他塞进去
        var mode = string.IsNullOrEmpty(o.GameMode) ? OsuApi.ModeList[0] : o.GameMode;

        return new OsuCommandParser.OsuCommand(
            o.OsuUserName, command.BpRank, command.Mode ?? OsuApi.ModeList.IndexOf(mode));
    }
}