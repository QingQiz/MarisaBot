using Marisa.Database;
using Marisa.Database.Entity.Plugin.Osu;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Osu;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.Osu;

[MarisaPlugin(PluginPriority.Osu)]
[MarisaPluginDoc("音游 **osu!** 的相关功能", "使用 `[name][#rank][:mode]` 指定查询目标")]
[MarisaPluginCommand("osu!", "osu！", "osu", "o", "!", "！")]
public partial class Osu : MarisaPluginBase, IMarisaPluginWithHelp, IHandleCommonException
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

    private static async Task<OsuCommandParser.OsuCommand?> ResolveCommand(Message message, bool withBpRank, bool allowRange)
    {
        var command = await ParseCommand(message, withBpRank, allowRange);

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return null;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            message.Reply("未绑定，请使用 `osu! bind 用户名` 绑定");
            return null;
        }

        if (command.Rank == null)
        {
            command = command.Mode == null
                ? new OsuCommandParser.OsuCommand(command.Name, null, 3)
                : new OsuCommandParser.OsuCommand(command.Name, null, command.Mode);
        }

        return command;
    }

    private static async Task<OsuCommandParser.OsuCommand?> ParseCommand(Message message, bool withBpRank, bool allowRange)
    {
        var command = OsuCommandParser.Parse(message.Command.ToString());

        if (command == null)
        {
            return null;
        }

        if (command.Rank != null && !withBpRank)
        {
            return null;
        }

        if (command.Rank?.End != null && !allowRange)
        {
            return null;
        }

        // 没设置名字，那就是查自己或者查@的人
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            using var realm = BotDbContext.OpenRealm();

            OsuBind? bind;
            // @了人
            if (message.MessageChain?.Messages.FirstOrDefault(m => m.Type == MessageDataType.At) is MessageDataAt at)
            {
                bind = realm.All<OsuBind>().FirstOrDefault(o => o.UserId == at.Target);
            }
            else
            {
                bind = realm.All<OsuBind>().FirstOrDefault(o => o.UserId == message.Sender.Id);
            }

            // 没找到证明不知道查谁的
            if (bind == null)
            {
                return command;
            }

            // 设置GameMode
            var mode = command.Mode ?? OsuApi.ModeList.IndexOf(bind.GameMode);

            return new OsuCommandParser.OsuCommand(bind.OsuUserName, command.Rank, mode);
        }

        // 有设置名字，那么要检查GameMode
        // 设置了mode的话就直接返回
        if (command.Mode != null) return command;

        // 从数据库取该用户名的 GameMode（在 await 前关闭 realm，避免跨线程访问 Realm）
        string? gameMode;
        using (var realm = BotDbContext.OpenRealm())
        {
            gameMode = realm.All<OsuBind>().FirstOrDefault(o => o.OsuUserName == command.Name)?.GameMode;
        }

        // 没被绑定，说明我们不知道这个人的GameMode，需要请求OsuApi来拿
        if (gameMode == null)
        {
            var u = await OsuApi.GetUserInfoByName(command.Name);
            return new OsuCommandParser.OsuCommand(command.Name, command.Rank, OsuApi.ModeList.IndexOf(u.Playmode.ToLower()));
        }

        return new OsuCommandParser.OsuCommand(command.Name, command.Rank, OsuApi.ModeList.IndexOf(gameMode));
    }

    /// <summary>
    ///     大部分请求都是已经绑定的用户发出的，所以这里直接从数据库里取，不行再请求
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static async Task<long> GetOsuIdByName(string name)
    {
        // 在 await 前关闭 realm，避免续体在别的线程 dispose Realm（Realm 线程亲和）
        long? boundId;
        using (var realm = BotDbContext.OpenRealm())
        {
            boundId = realm.All<OsuBind>().FirstOrDefault(o => o.OsuUserName == name)?.OsuUserId;
        }

        if (boundId != null) return boundId.Value;

        var id = await OsuApi.GetUserInfoByName(name);

        return id.Id;
    }

    public override Task BackgroundService(CancellationToken cancellationToken)
    {
        // 1200/minute, with burst capability of up to 200 beyond that
        // > 60/minute, you should probably give peppy a yell
        return Task.CompletedTask;
        /*
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now  = DateTime.Now;
                var next = now.AddDays(1);

                await Task.Delay(new DateTime(next.Year, next.Month, next.Day) - now, cancellationToken);

                using var realm = BotDbContext.OpenRealm();

                var tasks = new Queue<(string OsuUserName, int i, long id)>();

                foreach (var bind in realm.All<OsuBind>())
                {
                    tasks.Enqueue((bind.OsuUserName, OsuApi.ModeList.IndexOf(bind.GameMode), bind.OsuUserId));
                }

                while (tasks.Count != 0 && !cancellationToken.IsCancellationRequested)
                {
                    var task = tasks.Dequeue();

                    try
                    {
                        var result = await OsuApi.GetUserInfoByName(task.OsuUserName, task.i);
                        realm.Write(() => realm.AddWithAutoId(new OsuUserHistory
                        {
                            OsuUserName  = task.OsuUserName,
                            OsuUserId    = task.id,
                            Mode         = task.i,
                            UserInfo     = result.ToJson(),
                            CreationTime = DateTimeOffset.Now
                        }));
                    }
                    catch (FlurlHttpException e) when (e.StatusCode != 404)
                    {
                        tasks.Enqueue(task);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        // ReSharper disable once FunctionNeverReturns
        */
    }

    public override Task ExceptionHandler(Exception exception, Message message)
    {
        return CommonExceptionHandler.HandleCommonExceptionOr(exception, message, () => base.ExceptionHandler(exception, message));
    }
}
