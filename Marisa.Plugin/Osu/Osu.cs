using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Osu;
using Marisa.Plugin.Shared.FSharp.Osu;
using Marisa.Plugin.Shared.Osu;

namespace Marisa.Plugin.Osu;

public partial class Osu
{
    private static readonly HashSet<string> Debounce = [];

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
                message.Reply(new[] { "别急", "你先别急", "有点急", "你急也没用" }.RandomTake());
                return true;
            }

            Debounce.Add(name);
            return false;
        }
    }

    private static bool TryParseCommand(Message message, bool withBpRank, bool allowRange, out OsuCommandParser.OsuCommand? command)
    {
        command = ParseCommand(message, withBpRank, allowRange);

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            message.Reply("未绑定，请使用 `osu! bind 用户名` 绑定");
            return false;
        }

        if (command.BpRank == null)
        {
            command = command.Mode == null
                ? new OsuCommandParser.OsuCommand(command.Name, null, 3)
                : new OsuCommandParser.OsuCommand(command.Name, null, command.Mode);
        }

        return true;
    }

    private static OsuCommandParser.OsuCommand? ParseCommand(Message message, bool withBpRank, bool allowRange)
    {
        var command = OsuCommandParser.parser(message.Command.ToString())?.Value;

        if (command == null)
        {
            return null;
        }

        if (command.BpRank != null && !withBpRank)
        {
            return null;
        }

        if (command.BpRank?.Value.Item2 != null && !allowRange)
        {
            return null;
        }

        var db = new BotDbContext();

        // 没设置名字，那就是查自己或者查@的人
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            OsuBind? bind;
            // @了人
            if (message.MessageChain?.Messages.FirstOrDefault(m => m.Type == MessageDataType.At) is MessageDataAt at)
            {
                bind = db.OsuBinds.FirstOrDefault(o => o.UserId == at.Target);
            }
            else
            {
                bind = db.OsuBinds.FirstOrDefault(o => o.UserId == message.Sender.Id);
            }

            // 没找到证明不知道查谁的
            if (bind == null)
            {
                return command;
            }

            // 设置GameMode
            var mode = command.Mode?.Value ?? OsuApi.ModeList.IndexOf(bind.GameMode);

            return new OsuCommandParser.OsuCommand(bind.OsuUserName, command.BpRank, mode);
        }
        // 有设置名字，那么要检查GameMode
        else
        {
            // 设置了mode的话就直接返回
            if (command.Mode != null) return command;

            var bind = db.OsuBinds.FirstOrDefault(o => o.OsuUserName == command.Name);

            // 没被绑定，说明我们不知道这个人的GameMode，需要请求OsuApi来拿
            if (bind == null)
            {
                var u = OsuApi.GetUserInfoByName(command.Name).Result;
                return new OsuCommandParser.OsuCommand(command.Name, command.BpRank, OsuApi.ModeList.IndexOf(u.Playmode.ToLower()));
            }

            return new OsuCommandParser.OsuCommand(command.Name, command.BpRank, OsuApi.ModeList.IndexOf(bind.GameMode));
        }
    }

    /// <summary>
    ///     大部分请求都是已经绑定的用户发出的，所以这里直接从数据库里取，不行再请求
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

    public override async Task BackgroundService()
    {
        // 1200/minute, with burst capability of up to 200 beyond that
        // > 60/minute, you should probably give peppy a yell
        while (true)
        {
            var now  = DateTime.Now;
            var next = now.AddDays(1);

            await Task.Delay(new DateTime(next.Year, next.Month, next.Day) - now);

            var db = new BotDbContext();

            var tasks = new Queue<(string OsuUserName, int i, long id)>();

            foreach (var bind in db.OsuBinds)
            {
                tasks.Enqueue((bind.OsuUserName, OsuApi.ModeList.IndexOf(bind.GameMode), bind.OsuUserId));
            }

            while (tasks.Count != 0)
            {
                var task = tasks.Dequeue();

                try
                {
                    var result = await OsuApi.GetUserInfoByName(task.OsuUserName, task.i);
                    await db.OsuUserHistories.AddAsync(new OsuUserHistory
                    {
                        OsuUserName  = task.OsuUserName,
                        OsuUserId    = task.id,
                        Mode         = task.i,
                        UserInfo     = result.ToJson(),
                        CreationTime = DateTime.Now
                    });
                    await db.SaveChangesAsync();
                }
                catch (FlurlHttpException e) when (e.StatusCode != 404)
                {
                    tasks.Enqueue(task);
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public override Task ExceptionHandler(Exception exception, Message message)
    {
        switch (exception)
        {
            case not null:
                message.Reply(exception.GetType().Name + ": " + exception.Message);
                break;
        }
        return Task.CompletedTask;
    }
}