using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.FSharp.Osu;
using Marisa.Plugin.Shared.Osu;

namespace Marisa.Plugin.Osu;

public partial class Osu
{
    private static readonly HashSet<string> Debounce = new();

    private static void DebounceCancel(string name)
    {
        lock (Debounce)
        {
            Debounce.Remove(name);
        }
    }

    private static bool DebounceCheck(Message message, string name)
    {
        lock (Debounce)
        {
            if (Debounce.Contains(name))
            {
                message.Reply(new[] { "别急", "你先别急", "急你妈", "有点急", "你急也没用" }.RandomTake());
                return true;
            }

            Debounce.Add(name);
            return false;
        }
    }

    private static bool TryParseCommand(Message message, bool withBpRank, out OsuCommandParser.OsuCommand? command)
    {
        command = ParseCommand(message, withBpRank);

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            message.Reply("您未绑定，请使用 osu! bind <用户名> 绑定");
            return false;
        }

        if (command.BpRank == null)
        {
            command = command.Mode == null
                ? new OsuCommandParser.OsuCommand(command.Name, 1, 3)
                : new OsuCommandParser.OsuCommand(command.Name, 1, command.Mode);
        }

        return true;
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

    /// <summary>
    /// 大部分请求都是已经绑定的用户发出的，所以这里直接从数据库里取，不行再请求
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static async Task<long> GetOsuIdByName(string name)
    {
        var db = new BotDbContext();
        
        var u = db.OsuBinds.FirstOrDefault(o => o.OsuUserName == name);
        
        if (u != null) return u.OsuUserId;
        
        var id = await OsuApi.GetUserInfoByName(name);

        return id.Id;
    }
}