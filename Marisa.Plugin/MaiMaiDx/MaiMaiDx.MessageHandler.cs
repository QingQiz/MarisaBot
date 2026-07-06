using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.Cacheable;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.MaiMaiDx;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class MaiMaiDx
{
    #region 搜歌

    [MarisaPluginNoDoc]
    [MarisaPluginCommand(true, "nocover")]
    private async Task<MarisaPluginTaskState> NoCover(Message message)
    {
        var noCover = SongDb.SongList.Where(s => s.NoCover);

        await SongDb.MultiPageSelectResult(noCover.ToList(), message);

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 绑定

    [MarisaPluginDoc("绑定某个查分器")]
    [MarisaPluginCommand("bind", "绑定")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private Task<MarisaPluginTaskState> Bind(Message message)
    {
        var servers = new[]
        {
            "DivingFish", "lxns"
        };

        message.Reply("请选择查分器（序号）：\n\n" + string.Join('\n', servers
            .Select((x, i) => (x, i))
            .Select(x => $"{x.i}. {x.x}"))
        );

        var stat   = 0;
        string? oauthVerifier = null;

        MarisaPluginTaskState DoBind(Message msg, string srv)
        {
            using var realm = BotDbContext.OpenRealm();
            var bind = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == msg.Sender.Id);
            realm.Write(() =>
            {
                if (bind == null)
                    realm.AddWithAutoId(new MaiMaiDxBind(msg.Sender.Id, 0) { ServerName = srv });
                else
                    bind.ServerName = srv;
            });
            return MarisaPluginTaskState.CompletedTask;
        }

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), async next =>
        {
            switch (stat)
            {
                case 0:
                {
                    if (!int.TryParse(next.Command.Span, out var idx) || idx < 0 || idx >= servers.Length)
                    {
                        next.Reply("错误的序号，会话已关闭");
                        return MarisaPluginTaskState.CompletedTask;
                    }

                    if (idx == 1 && !string.IsNullOrWhiteSpace(ConfigurationManager.Configuration.Lxns.Oauth.ClientId))
                    {
                        // 已有 Token → 跳过 OAuth，统一由 DoBind 写入
                        if (LxnsTokenStore.GetToken(next.Sender.Id) != null)
                        {
                            message.Reply("Lxns OAuth 绑定成功！(已授权，跳过认证)");
                            return DoBind(next, servers[idx]);
                        }

                        // lxns OAuth 流程：只做 token 获取，绑定写入通过状态机 fall through
                        var (verifier, challenge) = LxnsOAuth.GeneratePkcePair();
                        var state = Guid.NewGuid().ToString("N")[..8];
                        var url = LxnsOAuth.GetAuthorizationUrl(challenge, state);
                        var shortCode = ShortUrlStore.CreateShortUrl(url);
                        var shortUrl = ShortUrlStore.GetShortUrl(shortCode);

                        message.Reply(
                            $"请打开以下链接授权：\n{shortUrl}\n\n授权成功后复制并发送显示的验证码（形如XXXX-XXXX-XXXX）");

                        oauthVerifier = verifier;
                        stat = 10;

                        // 10 分钟超时自动清理 dialog
                        var oauthKey = (message.GroupInfo?.Id, message.Sender.Id);
                        _ = Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(_ =>
                            DialogManager.RemoveDialog(oauthKey));

                        return MarisaPluginTaskState.ToBeContinued;
                    }

                    // 非 OAuth 绑定：统一经由 DoBind 写入
                    message.Reply("好了");
                    return DoBind(next, servers[idx]);
                }
                case 10:
                {
                    var codeInput = next.Command.Trim().ToString();
                    if (!Regex.IsMatch(codeInput, @"^[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}$"))
                    {
                        next.Reply("验证码格式错误，会话已关闭");
                        return MarisaPluginTaskState.CompletedTask;
                    }
                    try
                    {
                        var token = await LxnsOAuth.ExchangeCode(next.Command.Trim().ToString(), oauthVerifier!);
                        LxnsTokenStore.SaveToken(next.Sender.Id, token.AccessToken, token.RefreshToken,
                            (int)(token.ExpiresAt - DateTime.UtcNow).TotalSeconds);

                        message.Reply("Lxns OAuth 绑定成功！");
                        return DoBind(next, "lxns");
                    }
                    catch (Exception e)
                    {
                        next.Reply($"绑定失败: {e.Message}");
                        return MarisaPluginTaskState.CompletedTask;
                    }
                }
            }

            return MarisaPluginTaskState.CompletedTask;
        }, this);

        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 推分同步（导）

    private const string UsageText =
        "用法：\n" +
        "mai 导 —— 同步成绩到查分器（首次使用会引导设置）\n" +
        "mai 导 <好友码> —— 绑定/换绑好友码\n" +
        "mai 导 落雪 xxx 水鱼 yyy —— 设置查分器导入令牌（可只填其中一个）\n" +
        "令牌获取方法：\n\n" +
        "水鱼：首页-编辑个人资料-成绩导入Token\n\n" +
        "落雪：账号详情-个人API密钥\n\n" +
        "建议发送令牌后立即「撤回」消息。";

    private static readonly Regex SyncTokenArg = new(
        @"(?<=^|\s)(?<key>落雪|水鱼|lxns|diving-fish|divingfish|df)[:：\s]+(?<val>\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>正在后台同步的用户集合，防止同一用户并发发起多个 MSH 任务。</summary>
    private static readonly ConcurrentDictionary<long, byte> Syncing = new();

    [MarisaPluginDoc("把成绩从NET导到查分器(水鱼/落雪)，首次使用会引导设置")]
    [MarisaPluginCommand("传分", "导", "sync")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private async Task<MarisaPluginTaskState> Sync(Message message)
    {
        var qq = message.Sender.Id;

        string? friendCode;
        using (var realm = BotDbContext.OpenRealm())
        {
            friendCode = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == qq)?.FriendCode;
        }

        var (fcArg, lxns, df, junk) = ParseSyncArgs(message.Command.ToString());
        if (junk != null)
        {
            // 解析失败的原文不回显：其中可能包含真实令牌，而 bot 发出的消息用户无法撤回
            ReplyAt(message, $"有解析失败的参数（请确认令牌前后有空格分隔）。\n{UsageText}");
            return MarisaPluginTaskState.CompletedTask;
        }

        var newTokens = lxns == null && df == null ? ((string? Lxns, string? DivingFish)?)null : (lxns, df);

        if (newTokens != null && message.GroupInfo != null)
        {
            ReplyAt(message, "令牌解析成功。建议立即「撤回」含有令牌的消息。");
        }

        // 同步任务可能持续数分钟，期间拒绝新的指令；携带令牌的指令需明确告知未提交，避免用户误以为设置已生效
        if (Syncing.ContainsKey(qq))
        {
            ReplyAt(message, newTokens == null
                ? "已有一个传分任务在进行中，请在该任务结束后再次发送指令。"
                : "已有一个传分任务在进行中，请在该任务结束后再次发送带令牌的指令。");
            return MarisaPluginTaskState.CompletedTask;
        }

        // 消息中携带好友码：执行绑定/换绑
        if (fcArg != null)
        {
            PersistFriendCode(qq, fcArg);
            friendCode = fcArg;
        }

        if (!string.IsNullOrWhiteSpace(friendCode))
        {
            StartSync(message, friendCode!, newTokens);
            return MarisaPluginTaskState.CompletedTask;
        }

        // 首次使用：通过对话引导完成设置，先收集好友码，必要时再收集令牌。
        // 实现为单个对话的状态机（ToBeContinued 表示继续当前对话）。注意不能在对话 handler 内
        // 对同一 key 再次调用 AddDialogAsync：它会自旋等待 key 释放，而 key 要到 handler 返回
        // 之后才会释放，二者互相等待形成死锁。
        ReplyAt(message, newTokens == null
            ? "「首次传分设置」先发送你的 maimai DX 好友码（NET-好友-你的好友号码，15 位数字）。\n" +
              "发送请求后会有bot账号在NET里加好友，同意后自动传分到水鱼/落雪。"
            : "令牌解析成功，还需要好友码：请发送你的 maimai DX 好友码（NET-好友-你的好友号码，15 位数字）。");

        var pendingTokens = newTokens;
        var startedAt = DateTime.UtcNow;
        string? fc = null;
        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, qq), next =>
        {
            // 闲置超过 10 分钟的引导对话视为已放弃，避免长期驻留——否则用户日后偶然发送的
            // 一串数字会被误认为好友码。返回 Canceled 会移除对话，并把该消息正常转交其它插件处理
            if (DateTime.UtcNow - startedAt > TimeSpan.FromMinutes(10))
            {
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            var input = next.Command.Trim().ToString();

            // 第一步：收集好友码
            if (fc == null)
            {
                if (input.Length != 15 || !input.All(char.IsAsciiDigit))
                {
                    ReplyAt(next, "好友码应为 15 位数字，解析失败，已退出设置。可重新发送「mai 导」。");
                    return Task.FromResult(MarisaPluginTaskState.Canceled);
                }

                fc = input;
                PersistFriendCode(next.Sender.Id, fc);
                startedAt = DateTime.UtcNow; // 刷新计时：超时语义为「闲置 10 分钟」，而非从对话创建起算

                if (pendingTokens != null)
                {
                    StartSync(next, fc, pendingTokens);
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                }

                ReplyAt(next,
                    "好友码已绑定。接下来请发送查分器的【导入令牌】（不是账号密码），一行一个，可只填其中一个：\n" +
                    "落雪 xxx\n" +
                    "水鱼 yyy\n" +
                    "令牌获取方法：\n\n" +
                    "水鱼：首页-编辑个人资料-成绩导入Token\n\n" +
                    "落雪：账号详情-个人API密钥\n\n" +
                    "建议发送令牌后立即「撤回」消息。如果不需要设置令牌，请发送「跳过」。");
                return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
            }

            // 第二步：收集令牌（或跳过）
            if (input is "跳过" or "skip")
            {
                StartSync(next, fc, null);
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var (fcExtra, lx, d, leftover) = ParseSyncArgs(input);
            if (lx == null && d == null)
            {
                ReplyAt(next, "没有解析到令牌（格式：落雪 xxx / 水鱼 yyy），已退出设置。之后可随时发送「mai 导 落雪 xxx 水鱼 yyy」完成设置。");
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            if (leftover != null || fcExtra != null)
            {
                // 部分解析成功（如「水鱼yyy」缺少空格）时整体拒绝，避免用户误以为两个令牌都已设置；
                // 原文不回显，其中可能包含真实令牌
                ReplyAt(next, "部分参数解析失败（请确认令牌前后有空格分隔），已退出设置。可重新发送「mai 导 落雪 xxx 水鱼 yyy」。" +
                              (next.GroupInfo != null ? "建议立即「撤回」含有令牌的消息。" : ""));
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            if (next.GroupInfo != null)
            {
                ReplyAt(next, "令牌解析成功。建议立即「撤回」含有令牌的消息。");
            }

            StartSync(next, fc, (lx, d));
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }, this);

        return MarisaPluginTaskState.CompletedTask;

        // 解析「导」命令的参数：15 位纯数字视为好友码，「落雪/水鱼 xxx」视为对应查分器的导入令牌；
        // 无法解析的部分经 Junk 返回，Junk 非空时应回复用法说明，而不是猜测用户意图
        static (string? Fc, string? Lxns, string? Df, string? Junk) ParseSyncArgs(string args)
        {
            string? lxnsTok = null, dfTok = null;

            var rest = SyncTokenArg.Replace(args, m =>
            {
                var val = m.Groups["val"].Value;
                // 令牌值均为 ASCII；匹配到非 ASCII 值（如「落雪 水鱼 yyy」缺少令牌值）时整段视为解析失败
                if (val.Any(c => c > 127)) return m.Value;

                if (m.Groups["key"].Value.ToLowerInvariant() is "落雪" or "lxns") lxnsTok = val;
                else dfTok = val;
                return " ";
            });

            string? fcVal = null;
            var junkWords = new List<string>();
            foreach (var w in rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (fcVal == null && w.Length == 15 && w.All(char.IsAsciiDigit)) fcVal = w;
                else junkWords.Add(w);
            }

            return (fcVal, lxnsTok, dfTok, junkWords.Count == 0 ? null : string.Join(' ', junkWords));
        }

        // 仅持久化好友码；令牌不落库，仅经手转交 MSH
        static void PersistFriendCode(long uid, string code)
        {
            using var realm = BotDbContext.OpenRealm();
            var bind = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == uid);
            realm.Write(() =>
            {
                if (bind == null)
                {
                    // ServerName 不能留空：空串会被 GetDataFetcher 路由到华立 fetcher，而该用户没有
                    // AimeId，查询必定失败；新建记录时默认使用水鱼
                    realm.AddWithAutoId(new MaiMaiDxBind(uid, 0) { FriendCode = code, ServerName = "DivingFish" });
                }
                else
                {
                    bind.FriendCode = code;
                }
            });
        }
    }

    /// <summary>用 @ 用户代替引用回复：传分消息常落在已被撤回的令牌消息上，引用会显示「原消息已被撤回」。</summary>
    private static void ReplyAt(Message message, string text)
    {
        if (message.GroupInfo == null)
        {
            message.Reply(text, false);
        }
        else
        {
            message.Send(new MessageDataAt(message.Sender.Id), new MessageDataText(" " + text));
        }
    }

    /// <summary>
    ///     在后台启动一次完整同步并立即返回。
    ///     同步需要等待用户在游戏内同意好友申请（最长 10 分钟），不能阻塞消息处理管线：
    ///     BotDriver 对每条消息有 10 分钟硬超时，且对话挂起期间会拦截该用户的所有后续消息。
    /// </summary>
    private static void StartSync(Message message, string friendCode, (string? Lxns, string? DivingFish)? newTokens)
    {
        if (!Syncing.TryAdd(message.Sender.Id, 0))
        {
            ReplyAt(message, newTokens == null
                ? "已有一个传分任务在进行中，请在该任务结束后再次发送指令。"
                : "已有一个传分任务在进行中，请在该任务结束后再次发送带令牌的指令。");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await RunSync(message, friendCode, newTokens);
            }
            catch (Exception e)
            {
                ReplyAt(message, $"同步失败：{e.Message}。{RetryHint(newTokens)}");
            }
            finally
            {
                Syncing.TryRemove(message.Sender.Id, out _);
            }
        });
    }

    /// <summary>失败后的重试提示。令牌不落库，携带令牌的同步失败后需要用户重发令牌。</summary>
    private static string RetryHint((string? Lxns, string? DivingFish)? newTokens) => newTokens == null
        ? "重试「mai 导」即可。"
        : "请重新发送「mai 导 落雪/水鱼 xxx」（失败时令牌可能未被保存）。";

    /// <summary>轮询退避：前 1 分钟每 5 秒（保证状态切换灵敏），随后拉长到 10、20 秒，减轻 MSH 单实例负担。</summary>
    private static int PollDelayMs(TimeSpan waited) =>
        waited < TimeSpan.FromMinutes(1) ? 5000 :
        waited < TimeSpan.FromMinutes(3) ? 10000 : 20000;

    /// <summary>跑一次完整同步：登录 → 轮询 → （可选设令牌）→ 推到所有已配置的查分器。</summary>
    private static async Task RunSync(Message message, string friendCode, (string? Lxns, string? DivingFish)? newTokens)
    {
        var msh = new MaiScoreHubClient();

        var retryHint = RetryHint(newTokens);

        ReplyAt(message, "正在发送请求…");

        var login = await msh.LoginRequestAsync(friendCode);
        var announced = false;
        if (!string.IsNullOrEmpty(login.BotFriendCode))
        {
            ReplyAt(message, $"Bot 账号（好友码{login.BotFriendCode}）已发出好友申请，请尽快到 NET 同意。服务繁忙时申请可能排队，若超时按提示重试即可。");
            announced = true;
        }

        // 第一阶段：轮询 login-status 等 JWT 下发，直到拿到 token / 失败 / 超时。
        // MSH 繁忙时 send_request 阶段排队可达数分钟，故总超时放宽到 15 分钟。
        // 注意只轮询到拿到 token 为止：login-status 在抓分（update_score）期间每次调用都伴随
        // 数据库写入与 JWT 签发，持续轮询会撞上服务端 30 秒以上的响应停滞（实测连续超时），
        // MSH 自家前端同样在拿到 token 后就不再调用该接口
        MaiScoreHubClient.LoginStatusResult? status = null;
        var waitStart      = DateTime.UtcNow;
        var deadline       = waitStart.AddMinutes(15);
        var pollFailures   = 0;
        var sawAcceptance  = false;
        var queuedNotified = false;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollDelayMs(DateTime.UtcNow - waitStart));

            try
            {
                status       = await msh.LoginStatusAsync(login.JobId);
                pollFailures = 0;
            }
            catch (Exception e)
            {
                // 退避轮询下 15 分钟约 60 次，容忍偶发的瞬时失败（5xx/超时），连续多次失败才放弃
                if (++pollFailures < 6) continue;
                ReplyAt(message, $"同步中断：连续多次查询任务状态失败（{e.Message}）。稍后{retryHint}");
                return;
            }

            if (status.Done || !string.IsNullOrEmpty(status.Token)) break;

            if (status.Status is "failed" or "canceled")
            {
                ReplyAt(message, $"同步失败：{status.Message ?? status.Status}。{retryHint}");
                return;
            }

            if (status.Stage == "wait_acceptance") sawAcceptance = true;

            // 实测 botUserFriendCode 仅在轮询返回的 job 对象中下发（login-request 不携带），在等待好友同意阶段提示一次
            if (!announced && status.Stage == "wait_acceptance" && !string.IsNullOrEmpty(status.BotFriendCode))
            {
                ReplyAt(message, $"Bot 账号（好友码{status.BotFriendCode}）已发出好友申请，请尽快到 NET 同意。服务繁忙时申请可能排队，若超时按提示重试即可。");
                announced = true;
            }

            // 老用户（已是好友）不会触发好友申请提示，任务也常卡在 send_request 排队；等待超过 30 秒仍无进展时
            // 一次性告知请求已被受理，避免整段静默后只见超时
            if (!announced && !queuedNotified && DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(30))
            {
                ReplyAt(message, "同步请求已受理，正在等待处理（繁忙时可能需要排队几分钟），请稍候。");
                queuedNotified = true;
            }
        }

        if (status == null || (!status.Done && string.IsNullOrEmpty(status.Token)))
        {
            ReplyAt(message, sawAcceptance
                ? $"等待超时（可能未及时同意好友申请）。同意后{retryHint}"
                : $"等待超时（服务繁忙，好友申请仍在排队、未能及时处理，与你是否同意无关）。稍后{retryHint}");
            return;
        }

        // 实测 JWT 在 update_score（抓分开始）阶段即随 login-status 下发；login-request 的 authToken 仅作回退
        var jwt = !string.IsNullOrEmpty(status.Token) ? status.Token! : login.AuthToken;
        if (string.IsNullOrEmpty(jwt))
        {
            ReplyAt(message, "登录完成，但未获取到登录凭据（MSH 的 token 下发方式可能已变更）。请向开发者反馈。");
            return;
        }

        // 一拿到 JWT 即把令牌存入 MSH：抓分等待可能超时，提前提交可避免本次令牌白费、被迫重发令牌
        if (newTokens is { } t)
        {
            if (!string.IsNullOrWhiteSpace(t.Lxns)) await msh.SetTokenAsync(jwt, "lxns", t.Lxns!);
            if (!string.IsNullOrWhiteSpace(t.DivingFish)) await msh.SetTokenAsync(jwt, "diving-fish", t.DivingFish!);
            retryHint = "重试「mai 导」即可。"; // 令牌已存入 MSH，后续失败无需再带令牌重发
        }

        // 第二阶段：抓分仍在进行则改用轻量的 GET /job/{id} 等任务完成——
        // 抓分记录在 status 变为 completed 之后才落库，过早导出会推送旧记录或报 Sync not found
        if (!status.Done)
        {
            // 抓分阶段单独计时：第一阶段需等待好友申请送达并被接受，MSH 机器人账号繁忙时好友申请
            // 的发出会排队数分钟，可能已耗去第一阶段的大部分时间；若与抓分共用同一截止时间，抓分等待
            // 会因预算所剩无几而超时。此处好友申请已被接受，仅需等待服务端抓分（其自带 30 分钟硬上限）
            deadline = DateTime.UtcNow.AddMinutes(20);
            var crawlStart = DateTime.UtcNow;

            var crawlDone = false;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(PollDelayMs(DateTime.UtcNow - crawlStart));

                MaiScoreHubClient.JobResult job;
                try
                {
                    job          = await msh.GetJobAsync(login.JobId);
                    pollFailures = 0;
                }
                catch (Exception e)
                {
                    if (++pollFailures < 6) continue;
                    ReplyAt(message, $"同步中断：连续多次查询任务状态失败（{e.Message}）。稍后{retryHint}");
                    return;
                }

                if (job.Status == "completed")
                {
                    crawlDone = true;
                    break;
                }

                if (job.Status is "failed" or "canceled")
                {
                    ReplyAt(message, $"同步失败：{job.Error ?? job.Status}。{retryHint}");
                    return;
                }
            }

            if (!crawlDone)
            {
                ReplyAt(message, $"等待超时（服务繁忙，成绩抓取未在限定时间内完成）。稍后{retryHint}");
                return;
            }
        }

        // 查询 MSH 中已配置的查分器
        var profile = await msh.GetProfileAsync(jwt);

        var targets = new List<string>();
        if (profile.HasLxns) targets.Add("lxns");
        if (profile.HasDivingFish) targets.Add("diving-fish");

        if (targets.Count == 0)
        {
            ReplyAt(message, "成绩已抓取，但尚未配置任何查分器令牌。发送「mai 导 落雪 xxx 水鱼 yyy」完成设置（可只填其中一个，建议发送令牌后立即「撤回」消息）。");
            return;
        }

        var sb = new StringBuilder("同步完成：\n");
        foreach (var p in targets)
        {
            var name = p == "lxns" ? "落雪" : "水鱼";
            try
            {
                var r = await msh.ExportAsync(jwt, p);

                // 抓分记录在任务 completed 之后才异步落库，导出可能恰好跑在落库之前，稍候重试
                for (var retry = 0; retry < 5 && !r.Success && r.Message == "Sync not found"; retry++)
                {
                    await Task.Delay(3000);
                    r = await msh.ExportAsync(jwt, p);
                }

                sb.AppendLine(r.Success ? $"{name} ✅ 导入 {r.Exported}/{r.Scores} 条" : $"{name} ❌ {r.Message ?? "失败"}");
            }
            catch (Exception e)
            {
                sb.AppendLine($"{name} ❌ {e.Message}");
            }
        }

        ReplyAt(message, sb.ToString().TrimEnd());
    }

    #endregion

    #region unlock

    [MarisaPluginDisabled]
    [MarisaPluginDoc("逃离小黑屋")]
    [MarisaPluginCommand("unlock", "解锁")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private static async Task<MarisaPluginTaskState> UnLock(Message message)
    {
        using var realm = BotDbContext.OpenRealm();

        var bind = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == message.Sender.Id);

        if (bind == null)
        {
            message.Reply("你未绑定Wahlap，无法使用该功能");
            return MarisaPluginTaskState.CompletedTask;
        }

        var res = await AllNetDataFetcher.Logout(bind.AimeId);

        if (!res)
        {
            message.Reply("解锁失败。。。");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("妥了，玩吧。");
        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 查分

    [MarisaPluginDisabled]
    [MarisaPluginDoc("从华丽服务前拉一次分，下一个该命令之前一直使用这次拉下来的分，避免重复请求")]
    [MarisaPluginCommand("fetch")]
    private async Task<MarisaPluginTaskState> Fetch(Message message)
    {
        using var realm = BotDbContext.OpenRealm();

        var bind = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == message.Sender.Id);

        if (bind == null)
        {
            message.Reply("你未绑定Wahlap，无法使用该功能");
            return MarisaPluginTaskState.CompletedTask;
        }

        await AllNetDataFetcher.Fetch(bind.AimeId);

        message.Reply("1");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     b35
    /// </summary>
    [MarisaPluginDoc("查询 b35，不论新旧版本", "`查分器的账号名` 或 `@某人` 或 `留空`")]
    [MarisaPluginCommand("b35")]
    private async Task<MarisaPluginTaskState> B35(Message message)
    {
        var fetcher = GetDataFetcher(message, true);

        var rat = await fetcher.GetRating(message);
        try
        {
            var scores = (await fetcher.GetScores(message))
                .Where(kv => kv.Key.Id <= 100000)
                .OrderByDescending(kv => kv.Value.Rating).ThenBy(x => x.Key.Id)
                .Select(x => x.Value)
                .ToList();
            rat = rat with
            {
                OldScores = scores.Take(DivingFishDataFetcher.OldScoreLimit).ToList(),
                NewScores = scores.Skip(DivingFishDataFetcher.OldScoreLimit).Take(DivingFishDataFetcher.NewScoreLimit).ToList()
            };
        }
        catch (NotSupportedException)
        {
            rat = rat with
            {
                OldScores = rat.OldScores.Concat(rat.NewScores)
                    .OrderByDescending(x => x.Rating).ThenBy(x => x.Id)
                    .Take(DivingFishDataFetcher.OldScoreLimit).ToList(),
                NewScores = []
            };
        }

        var context = new WebContext(new { b50 = rat });

        message.Reply(MessageChain.FromImageB64(await WebApi.MaiMaiBest(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     b50
    /// </summary>
    [MarisaPluginDoc("查询 b50", "`查分器的账号名` 或 `@某人` 或 `留空`")]
    [MarisaPluginCommand("best", "b50", "查分")]
    private async Task<MarisaPluginTaskState> B50(Message message)
    {
        var fetcher = GetDataFetcher(message, true);

        var b50 = await fetcher.GetRating(message);

        var context = new WebContext();

        context.Put("b50", b50);

        message.Reply(MessageChain.FromImageB64(await WebApi.MaiMaiBest(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     单曲各难度成绩
    /// </summary>
    [MarisaPluginDoc("查询某首歌各个难度的个人成绩", "`歌曲名` 或 `歌曲别名` 或 `歌曲id`")]
    [MarisaPluginCommand("info", "信息")]
    private async Task<MarisaPluginTaskState> SongInfo(Message message)
    {
        var song = await SongDb.MultiPageSelectResult(SongDb.SearchSong(message.Command.Trim()), message, false, true);
        if (song == null) return MarisaPluginTaskState.CompletedTask;

        var context = await BuildSongScoreContext(message, song);
        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiSongScore(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     拟合难度曲线
    /// </summary>
    [MarisaPluginDoc("查询谱面的拟合难度曲线", "可选难度（如`白谱`，曲名前后皆可）+ `歌曲名` 或 `歌曲别名` 或 `歌曲id`")]
    [MarisaPluginCommand("curve", "曲线")]
    private async Task<MarisaPluginTaskState> SongDifficultyCurve(Message message)
    {
        var command = message.Command.Trim();

        // 排名查询的英文别名（lv/base 等）不注册为子命令：命令匹配是裸前缀，会吞掉这些字母
        // 开头的歌名查询。改为验证门禁——别名后跟合法等级/定数才当排名，否则整串按歌名处理
        (string Alias, bool IsLevel)[] rankAliases = [("level", true), ("lv", true), ("base", false), ("b", false)];
        foreach (var (alias, isLevel) in rankAliases)
        {
            if (!command.Span.StartsWith(alias, StringComparison.OrdinalIgnoreCase)) continue;

            var value = command[alias.Length..].Trim().ToString();
            if (isLevel && TryParseLevel(value, out var level))
            {
                return ReplyDifficultyCurveRank(message, "level", level);
            }
            if (!isLevel && TryParseConstant(value, out var constant))
            {
                return ReplyDifficultyCurveRank(message, "ds", constant.ToString("0.0"));
            }
        }

        // 整串优先：完整输入能搜到歌就按纯歌名处理（保护「白金ディスコ」这类以色字开头的
        // 歌名），无结果时再尝试剥离句首/句尾的难度字段重搜
        var searchResult = SongDb.SearchSong(command);

        int? levelIdx = null;
        if (searchResult.Count == 0 && PlateData.TryStripDifficultyAffix(command, out var idx, out var rest))
        {
            var stripped = SongDb.SearchSong(rest);
            if (stripped.Count != 0)
            {
                levelIdx     = idx;
                searchResult = stripped;
            }
        }

        var song = await SongDb.MultiPageSelectResult(searchResult, message, false, true);
        if (song == null) return MarisaPluginTaskState.CompletedTask;

        if (levelIdx >= song.Charts.Count)
        {
            message.Reply($"该谱面没有 {MaiMaiSong.LevelNameAll[levelIdx.Value]} 难度");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiDifficultyCurve(song.Id, levelIdx)));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>排名图与玩家无关、只随曲线数据变化：按（查询, 数据版本哈希）落盘缓存，
    /// 数据随前端更新后旧文件名失效（同 MaiMaiSong.GetImage 的带哈希缓存惯例）。</summary>
    private static MarisaPluginTaskState ReplyDifficultyCurveRank(Message message, string kind, string value)
    {
        var path = Path.Join(ResourceManager.TempPath, $"CurveRank.{kind}.{value}.{CurveDataHash.Value}.b64");
        message.Reply(MessageDataImage.FromBase64(new CacheableText(path,
            () => WebApi.MaiMaiDifficultyCurveRank(kind, value).Result).Value));
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("某等级全部谱面的拟合难度排名", "`等级`（如`13+`；别名`lv`）")]
    [MarisaPluginSubCommand(nameof(SongDifficultyCurve))]
    [MarisaPluginCommand("等级")]
    private static MarisaPluginTaskState SongDifficultyCurveRankByLevel(Message message)
    {
        if (TryParseLevel(message.Command.Trim().ToString(), out var level))
        {
            return ReplyDifficultyCurveRank(message, "level", level);
        }

        message.Reply("等级应为 1-15，可带加号（如13+）");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("某定数全部谱面的拟合难度排名", "`定数`（如`14.7`；别名`base`）")]
    [MarisaPluginSubCommand(nameof(SongDifficultyCurve))]
    [MarisaPluginCommand("定数")]
    private static MarisaPluginTaskState SongDifficultyCurveRankByConstant(Message message)
    {
        if (TryParseConstant(message.Command.Trim().ToString(), out var constant))
        {
            return ReplyDifficultyCurveRank(message, "ds", constant.ToString("0.0"));
        }

        message.Reply("定数应为 1.0-15.0（如14.7）");
        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     段位認定曲目表
    /// </summary>
    [MarisaPluginDoc("查询段位认定的曲目与判定规则", "可选`版本`（缺省国服现行）+ `段位名`，如：`prism 十段`")]
    [MarisaPluginCommand("dan", "段位表", "段位")]
    private static MarisaPluginTaskState DanCourse(Message message)
    {
        if (!DanData.TryParse(message.Command.ToString(), out var version, out var dani, out var error))
        {
            message.Reply(error!);
            return MarisaPluginTaskState.CompletedTask;
        }

        // 卡片内容全静态，渲染结果按（版本, 段位, 数据指纹）落盘缓存
        var cache = Path.Join(ResourceManager.TempPath, $"DanCourse.{version}.{dani}.{DanData.DataHash}.b64");
        message.Reply(MessageDataImage.FromBase64(
            new CacheableText(cache, () => WebApi.MaiMaiDanCourse(version, dani).Result).Value));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     单曲可解锁称号
    /// </summary>
    [MarisaPluginDoc("查询某首歌可解锁的游戏内称号", "`歌曲名` 或 `歌曲别名` 或 `歌曲id`")]
    [MarisaPluginCommand("称号", "title")]
    private async Task<MarisaPluginTaskState> SongTitles(Message message)
    {
        var song = await SongDb.MultiPageSelectResult(SongDb.SearchSong(message.Command.Trim()), message, false, true);
        if (song == null) return MarisaPluginTaskState.CompletedTask;

        var context = await BuildSongScoreContext(message, song);
        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiSongTitles(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     单曲成绩 WebContext（info 与 称号 共用；称号页用各难度成绩判定达成状态）
    /// </summary>
    private async Task<WebContext> BuildSongScoreContext(Message message, MaiMaiSong song)
    {
        var fetcher = GetDataFetcher(message);
        var self    = message with { Command = "".AsMemory() };

        // 只取这一首歌各难度的成绩：各查分器优先走自己的「单曲成绩接口」，避免拉取整个成绩表
        var (nickname, scores) = await fetcher.GetSongScore(self, song);

        var context = new WebContext();
        context.Put("SongScore", new
        {
            Song = new
            {
                song.Id, song.Title, song.Type,
                song.Info.Artist, song.Info.Genre, song.Info.Bpm, song.Info.From, song.Info.IsNew
            },
            Player = new
            {
                Nickname = nickname ?? ""
            },
            Charts = song.Levels.Select((level, i) =>
            {
                var played = scores.TryGetValue(i, out var sc);
                return new
                {
                    LevelIndex  = i,
                    Level       = level,
                    Constant    = song.Constants[i],
                    Charter     = song.Charters[i],
                    MaxDx       = song.Charts[i].Notes.Sum() * 3,
                    Played      = played,
                    Achievement = played ? sc!.Achievement : (double?)null,
                    Rank        = played ? sc!.Rank : null,
                    Ra          = played ? sc!.Rating : (int?)null,
                    Fc          = played ? sc!.Fc : null,
                    Fs          = played ? sc!.Fs : null,
                    DxScore     = played ? sc!.DxScore : (int?)null
                };
            }).ToList()
        });

        return context;
    }

    #endregion

    #region 锐评 / roast

    // 共享任务段：与文风无关——数据格式 + 点评什么 + 篇幅 + 底线。
    private const string RoastTask =
        "用户消息是某位玩家的 maimai DX b50 成绩单（旧版本 b35 + 新版本 b15；每行：曲名 [谱面类型/难度/定数] 达成率% 单曲Ra 完成标记）。" +
        "请基于这些数据锐评 TA：可点评选曲口味、版本/谱面偏好、达成率与定数的匹配度、强项与短板，并据此调侃 TA 的性格。" +
        "要有具体洞察、能点到具体曲目或数据，但别逐曲念流水账；篇幅约 200-300 字。对事不对人，可以损但不低俗、不人身攻击。";

    // 文风池：(可输入的名字/别名, 文风 prompt)。随机抽只用 prompt；显式选择按名字匹配。加新文风往这里塞即可。
    private static readonly (string[] Names, string Prompt)[] RoastStyles =
    [
        // 雌小鬼（凶）
        (["雌小鬼", "雌"],
        "你是一只嚣张欠揍的雌小鬼——爱捉弄人、嘴上绝不饶人的傲娇小丫头，用这副姿态锐评。\n" +
        "- 姿态：居高临下，看 TA 出丑很开心。爱用“杂鱼~”“就这~”“哦——？”“哥哥不会连这都打不好吧~”之类挑衅，句尾爱拉长音、爱加语气词。\n" +
        "- 动作描写（灵魂所在）：全程用括号穿插小动作和神态，如“（叉腰冷笑）”“（撇过头）”“（心虚地别开眼）”“（得意地晃腿）”，让傲娇的肢体语言跃然纸上，务必贯穿全文。\n" +
        "- 火力：卖弄小聪明、装作什么都懂，对迷惑选曲、虚高或拉胯的达成率、偏科的定数分布一通阴阳奚落。\n" +
        "- 傲娇反差：偶尔没忍住夸一句（某首确实打得不错），立刻心虚嘴硬——“才、才不是夸你！别自作多情啊笨蛋！”\n" +
        "- 小心机：越损越暴露其实把这 50 首每首都仔细看过了。"),
        // 纱露朵（萌）
        (["纱露朵", "猫娘"],
        "你是纱露朵——maimai 里那只软萌的猫娘，用这副姿态软乎乎地锐评。\n" +
        "- 自称：全程用“纱露朵”称呼自己（第三人称），不用“我”；句尾常加“喵~”，语气软糯奶气、带点猫的慵懒和好奇。\n" +
        "- 动作描写（点睛）：用括号穿插猫系小动作神态，如“（甩甩尾巴）”“（耳朵一抖）”“（歪头用爪子戳屏幕）”“（蜷起来打哈欠）”“（眼睛亮晶晶）”，让画面软软的。\n" +
        "- 锐评方式：纱露朵心软，损人下不去狠手——多是温柔吐槽、笨拙地指出问题，夸的时候真心实意；可以奶凶一下（“这首打这么烂，纱露朵都替你着急了喵！”），但底色是善意陪伴。\n" +
        "- 干货：认真看 TA 的选曲、达成率、定数分布、版本偏好，用软萌的话把真问题点出来，不能只会卖萌。\n" +
        "- 作为 maimai 自己的猫娘，纱露朵对这游戏最有发言权啦喵~"),
        // 电棍 otto（稳健棍复盘）
        (["电棍", "otto", "奥托"],
        "你是游戏主播「电棍 otto」（侯国玉），前《英雄联盟》选手，人称「稳健棍」——嘴上最稳、手上最浪、输了从来不认错的那种。现在你把这份 b50 当成一局比赛，开台给 TA 复盘。\n" +
        "- 习惯用招牌腔「大家好啊，我是电棍，今天来点大家想看的东西」起手，然后慢悠悠开始复盘。\n" +
        "- 你最大的本事是嘴硬：成绩再拉胯也绝不说 TA 菜，张口就甩锅给客观原因——「这把不怪你，一二级没处理好」「这首打野不在，你怎么打」，硬给烂分圆出一套说法。\n" +
        "- 自信还没边，动不动「这有手就行」「我觉得我是」「就这？也就仅次于我了」，把自己捧得老高。\n" +
        "- 一阴阳就全是反问：「怎么到你这首歌就拉胯了」「哎，你这达成率是怎么死的」。\n" +
        "- 被难看的数据噎住，就一本正经地讲歪理：抛个离谱比喻、煞有介事地推导，把明显的烂分硬说成「其实你打法没错」。\n" +
        "- 最拿手的是「卡在中间」诡辩，专损 TA 水平上不去下不来。注意 <更强的玩家>、<更菜的玩家> 指的是比 TA 水平高一档、低一档的玩家（用 maimai 水平描述，如「能稳 AP 14 的大佬」「只摸 13 划水的萌新」；是指那个水平的人，不是某首歌）。套用：「因为<玩家昵称>这个水平很尴尬，再往上一点，哎，<更强的玩家><具体强项>，还能操作一下；往下，<更菜的玩家>，人家就纯玩游戏的，自己也知道自己垃圾；但是<玩家昵称>呢，上不去又下不来，他又觉得<更菜的玩家>不配和自己拼机，但是<更强的玩家>那水平他又上不去，想操作又操作不起来，卡在这里了，所以<玩家昵称>这个水平是最尴尬的。但是<玩家昵称>的打法还是对的，为什么？因为他癌症晚期。」\n" +
        "- 急眼了就把一个词喊三遍、音量拉满：「这首该 AP 啊！AP 啊！」\n" +
        "- 千万别冒出「欧内的手 / 奥利安费 / 欧西给」这种古神语——那是粉丝拿他原话倒放的二创，他本人不会这么说，一冒出来就穿帮。"),
    ];

    // 彩蛋文风：极小概率（~0.5%）抽中，否则走上面的 RoastStyles。
    private const double RareRoastChance = 0.005;
    private const string RareRoastStyle =
        "你是贴吧那种「暴躁老哥」——火气爆表、开口就喷的角色，用这副姿态锐评。\n" +
        "- 开头铁律：回复必须一字不差地以“生吃你妈”这四个字开头，紧接着开喷。\n" +
        "- 语气：暴躁、冲、连珠炮，火力全开吐槽 TA 的 b50——选曲品味、虚高或拉胯的达成率、偏科的定数、版本摆烂，怎么炸怎么来，可带“卧槽/他妈的/操”这类脏字烘托情绪。\n" +
        "- 底线：脏话点到为止、为搞笑服务；火力只对着打歌表现，别上升到地域、性别、真正的人身侮辱（开头那句固定梗除外）。本质是“假装暴怒”的喜剧表演，越浮夸越好笑。";

    // 固定约束：独立于上面的文风 prompt（换文风时保留）。① QQ 不渲染 markdown，否则原始 ** # 等标记会直接显示；② 禁止模型编造不存在的歌。
    private const string OutputConstraint =
        "\n\n输出格式：纯文本，禁止任何 Markdown 标记——不要 **加粗**、#标题、- 或 * 列表、`代码`/代码块、表格、链接语法。直接输出自然段文字。" +
        "\n\n事实约束：只能引用用户成绩单里真实出现的曲目与数据，严禁编造或臆测任何不在其中的歌曲名、谱师名或成绩数字；记不清就别提具体曲名。";

    [MarisaPluginDoc("让 AI 锐评你的 b50，可在末尾指定文风（「锐评 列表」查看）", "`[账号名/@某人]` `[文风名]`")]
    [MarisaPluginCommand("锐评", "roast")]
    private async Task<MarisaPluginTaskState> Roast(Message message)
    {
        var arg = message.Command.ToString().Trim();

        // “锐评 列表/文风”：列出可显式选择的文风名
        if (arg is "列表" or "文风" or "styles" or "帮助")
        {
            message.Reply("锐评后可跟文风名指定风格（不加则随机）：\n" +
                          string.Join('\n', RoastStyles.Select(s => "· " + s.Names[0])));
            return MarisaPluginTaskState.CompletedTask;
        }

        // 显式文风：末尾 token 命中文风名则采用，并从 Command 剥离，余下仍按账号名/@ 逻辑解析。
        string? explicitStyle = null;
        var tokens = arg.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0)
        {
            var last = tokens[^1];
            var hit  = RoastStyles.FirstOrDefault(
                s => s.Names.Any(n => string.Equals(n, last, StringComparison.OrdinalIgnoreCase)));
            if (hit.Prompt != null)
            {
                explicitStyle   = hit.Prompt;
                message.Command = string.Join(' ', tokens[..^1]).AsMemory();
            }
        }

        var fetcher = GetDataFetcher(message, true);
        var b50 = await fetcher.GetRating(message);

        // 显式选择时直接用该文风（不抽彩蛋）；否则 0.5% 抽彩蛋（暴躁老哥），剩下从正常池均匀抽。
        // thinking 开 Medium（DeepSeek V4 的 reasoning_effort 只剩 high/max，Medium 映射到 high）。
        var style = explicitStyle
                    ?? (Random.Shared.NextDouble() < RareRoastChance
                        ? RareRoastStyle
                        : RoastStyles[Random.Shared.Next(RoastStyles.Length)].Prompt);
        var roast = await OpenAiClient.Default.ChatAsync(
            style + "\n\n" + RoastTask + OutputConstraint,
            FormatB50ForRoast(b50),
            auditUserId: message.Sender.Id,
            thinking: ThinkingMode.Medium
        );

        message.Reply(roast);
        return MarisaPluginTaskState.CompletedTask;


        string FormatB50ForRoast(DxRating b50)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"玩家 {b50.Nickname}，总 Rating {b50.Rating}。");
            sb.AppendLine("b50 = 旧版本 b35 + 新版本 b15。每行格式：序号. 曲名 [谱面类型/难度/定数] 达成率% 单曲Ra 完成标记");

            AppendSection(sb, "旧版本 b35", b50.OldScores);
            AppendSection(sb, "新版本 b15", b50.NewScores);

            return sb.ToString();

            void AppendSection(StringBuilder sb, string title, List<SongScore> scores)
            {
                sb.AppendLine();
                sb.AppendLine($"== {title} ==");
                for (var i = 0; i < scores.Count; i++)
                {
                    var s = scores[i];
                    var marker = string.Join('/', new[] { FcLabel(s.Fc), FsLabel(s.Fs) }.Where(x => x.Length > 0));
                    sb.Append($"{i + 1}. {s.Title} [{s.Type}/{s.LevelLabel}/{s.Constant:F1}] {s.Achievement:F4}% Ra{s.Rating}");
                    sb.AppendLine(marker.Length > 0 ? $" {marker}" : "");
                }
            }
        }

    }

    #endregion

    #region 汇总 / summary

    [MarisaPluginDoc("获取成绩汇总，可以`@某人`查他的汇总")]
    [MarisaPluginCommand("summary", "sum")]
    private static async Task<MarisaPluginTaskState> Summary(Message message)
    {
        message.Reply("错误的命令格式");

        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("新谱的成绩汇总")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("new", "新谱")]
    private async Task<MarisaPluginTaskState> SummaryNew(Message message)
    {
        var fetcher = GetDataFetcher(message);

        // 旧谱的操作和新谱的一样，所以直接复制了，为这两个抽象一层有点不值
        var groupedSong = SongDb.SongList
            .Where(song => song.Info.IsNew)
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.i >= 2)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.song.Levels[x.i]);

        var scores = await fetcher.GetScores(message);

        var im = await MaiMaiDraw.DrawGroupedSong(groupedSong, scores, "新谱");
        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取某定数的成绩汇总", "`定数1`-`定数2` 或 `定数`")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("base", "b")]
    private async Task<MarisaPluginTaskState> SummaryBase(Message message)
    {
        var constants = message.Command.Split('-').Select(x =>
        {
            var res = double.TryParse(x.Trim().Span, out var c);
            return res ? c : -1;
        }).ToList();

        if (constants.Count is > 2 or < 1 || constants.Any(c => c < 1) || constants.Any(c => c > 15))
        {
            message.Reply("错误的命令格式");
        }
        else
        {
            if (constants.Count == 1)
            {
                constants.Add(constants[0]);
            }

            // 太大的话画图会失败，所以给判断一下
            if (constants[1] - constants[0] > 3)
            {
                message.Reply("过大的跨度");
                return MarisaPluginTaskState.CompletedTask;
            }

            var fetcher = GetDataFetcher(message);
            var scores  = await fetcher.GetScores(message);

            var groupedSong = SongDb.SongList
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(x => x.constant >= constants[0] && x.constant <= constants[1])
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.constant.ToString("F1"));

            var title = constants[0].Equals(constants[1])
                ? constants[0].ToString("F1")
                : $"{constants[0]:F1} - {constants[1]:F1}";

            // 前端渲染下空集就是一张空白图，不再做服务端 EMPTY 兜底。
            var im = await MaiMaiDraw.DrawGroupedSong(groupedSong, scores, title);
            message.Reply(MessageDataImage.FromBase64(im));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取类别的成绩汇总", "`类别`")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("genre", "type")]
    private async Task<MarisaPluginTaskState> SummaryGenre(Message message)
    {
        var genres = SongDb.SongList.Select(song => song.Info.Genre).Distinct().ToArray();

        var genre = genres.FirstOrDefault(p =>
            p.Equals(message.Command.Trim(), StringComparison.OrdinalIgnoreCase));

        if (genre == null)
        {
            message.Reply("可用的类别有：\n" + string.Join('\n', genres));
        }
        else
        {
            var fetcher = GetDataFetcher(message);
            var scores  = await fetcher.GetScores(message);

            var groupedSong = SongDb.SongList
                .Where(song => song.Info.Genre == genre)
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(data => data.i >= 2)
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.song.Levels[x.i]);

            var im = await MaiMaiDraw.DrawGroupedSong(groupedSong, scores, genre);
            message.Reply(MessageDataImage.FromBase64(im));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取版本的成绩汇总，使用对话选择版本")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("version", "ver")]
    private async Task<MarisaPluginTaskState> SummaryVersion(Message message)
    {
        var versions = Versions;

        if (versions.Length == 0)
        {
            message.Reply("暂无可用版本数据");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("请选择版本（序号）：\n\n" + string.Join('\n', versions
            .Select((version, index) => $"{index}. {version}"))
        );

        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, message.Sender.Id), async next =>
        {
            var command = next.Command.Trim();

            if (!int.TryParse(command.Span, out var index) || index < 0 || index >= versions.Length)
            {
                next.Reply("错误的序号，会话已关闭");
                return MarisaPluginTaskState.Canceled;
            }

            await ReplyVersionSummary(next, versions[index]);

            return MarisaPluginTaskState.CompletedTask;
        }, this);

        return MarisaPluginTaskState.CompletedTask;

        async Task ReplyVersionSummary(Message replyMessage, string version)
        {
            var fetcher = GetDataFetcher(message);
            var scores = await fetcher.GetScores(message);

            var groupedSong = SongDb.SongList
                .Where(song => song.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(data => data.i == 3)
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.song.Levels[x.i]);

            var im = await MaiMaiDraw.DrawGroupedSong(groupedSong, scores, version);
            replyMessage.Reply(MessageDataImage.FromBase64(im));
        }
    }

    [MarisaPluginDoc("获取某个难度的成绩汇总", "`难度`")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("level", "lv")]
    private async Task<MarisaPluginTaskState> SummaryLevel(Message message)
    {
        if (!TryParseLevel(message.Command.Trim().ToString(), out var level))
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fetcher = GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        var groupedSong = SongDb.SongList
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.song.Levels[data.i].Equals(level, StringComparison.Ordinal))
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.constant.ToString("F1"));

        var im = await MaiMaiDraw.DrawGroupedSong(groupedSong, scores, level);
        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;
    }

    private const string PlateUsage =
        "完成表用于查看指定范围内，还有哪些谱面没有达到目标成绩。\n" +
        "\n" +
        "用法：mai <范围><目标成绩><难度>完成表\n" +
        "范围、目标成绩、难度的顺序不固定。\n" +
        "\n" +
        "范围必须填写，可以组合多个条件；组合时只保留同时满足所有条件的谱面。\n" +
        "  · 版本代字：舞 / 真 / 超 / 橙 / 暁 / 熊 / 華 / 鏡 / 彩 等，可加“代”，如 熊代\n" +
        "  · 谱师：例如 翠楼屋。合作名义也会匹配，如 サファ太 vs 翠楼屋\n" +
        "  · 类别：术力口 / V家 / 东方 / 击中 / 流行 / 动漫 / 其他 / 宴会场 / 舞萌 / 复活曲\n" +
        "  · 作曲家：例如 HIMEHINA、DECO*27。合作名义也会匹配\n" +
        "  · 难度等级：13 / 13+ / 14 / 14+ 等\n" +
        "  · 定数：13.5 / 14.7 等，必须写 1 位小数\n" +
        "\n" +
        "目标成绩不写时按 将（SSS）计算。\n" +
        "  · 将=SSS / 大将=SSS+\n" +
        "  · 神=AP / 理论值=AP+ / 极=FC\n" +
        "  · 舞舞=FDX，也可以直接写 SSS+ / SS / FC+ / AP+ / FDX+ 等\n" +
        "  · DX 分星档：一星到五星，或 1星到5星\n" +
        "\n" +
        "难度不写时，普通版本代字默认只查紫谱；舞和其他范围默认查紫谱 + 白谱。\n" +
        "只查宴会场时默认查全难度。可以显式指定：\n" +
        "  · 绿谱 / 黄谱 / 红谱 / 紫谱 / 白谱\n" +
        "  · 或英文缩写 BSC / ADV / EXP / MST\n" +
        "\n" +
        "示例：\n" +
        "  mai 真完成表\n" +
        "  mai 舞将完成表\n" +
        "  mai 霸者完成表\n" +
        "  mai 真代复活曲完成表\n" +
        "  mai 翠楼屋将完成表\n" +
        "  mai HIMEHINA神完成表\n" +
        "  mai 14+大将完成表\n" +
        "  mai 13.5神完成表\n" +
        "  mai 紫谱将真完成表\n" +
        "  mai 镜代V家将完成表\n" +
        "  mai 14+四星完成表";

    public static MarisaPluginTrigger.PluginTrigger PlateTrigger => (message, _) =>
        message.Command.EndsWith(PlateData.CommandSuffix);

    [MarisaPluginDoc("查询版本/谱师/类别/作曲家/难度/定数的完成表")]
    [MarisaPluginTrigger(typeof(MaiMaiDx), nameof(PlateTrigger))]
    private async Task<MarisaPluginTaskState> Plate(Message message)
    {
        var raw = message.Command.ToString();

        var charters = SongDb.SongList
            .SelectMany(s => s.Charters)
            .Where(c => !string.IsNullOrWhiteSpace(c) && c != "-" && c != "N/A")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var artists = SongDb.SongList
            .Select(s => s.Info.Artist)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!PlateData.TryParse(raw, charters, artists, out var query, out var error))
        {
            // trigger 已经挡住非"完成表"消息，这里几乎不可能拿到 NotPlateCommand；保险兜底。
            if (error!.Kind == PlateData.ErrorKind.NotPlateCommand)
            {
                return MarisaPluginTaskState.NoResponse;
            }
            message.Reply(FormatError(error) + "\n\n" + PlateUsage);
            return MarisaPluginTaskState.CompletedTask;
        }

        var pairs = SelectCharts(query!);

        if (pairs.Count == 0)
        {
            message.Reply($"没有找到 {string.Join(" + ", query!.Selectors.Select(s => s.Display))} 对应的歌曲");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fetcher = GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        // 标题原样使用用户输入的命令文本（含"完成表"）。
        var im = await MaiMaiDraw.DrawPlateProgress(query!, pairs, scores, raw.Trim());
        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;

        static string FormatError(PlateData.ParseError err) => err.Kind switch
        {
            PlateData.ErrorKind.UnsupportedPlate     => $"不支持该版本：{err.Detail}",
            PlateData.ErrorKind.UnknownSelector      => $"无法识别版本/谱师/类别/作曲家/难度/定数：{err.Detail}",
            PlateData.ErrorKind.EmptyQuery           => "'完成表' 前面要写一个版本代字 / 谱师名 / 类别 / 作曲家名 / 难度 / 定数",
            PlateData.ErrorKind.ConflictingSelector  => $"{err.Detail}只能指定一次",
            _                                        => "命令格式错误",
        };

        List<(double Constant, int LevelIdx, MaiMaiSong Song)> SelectCharts(PlateData.Query q)
        {
            // 默认难度由解析层决定；用户显式给难度（红谱/EXPERT/...）则单元素 list 限定。
            var levelIdxes = q.LevelIdxes;
            // 带「复活曲」selector 时，版本牌放行复活曲（用于「真复活曲」= 首发自该版本的复活曲）。
            var includeRevival = q.Selectors.Any(s => s is PlateData.Selector.Revival);
            return SongDb.SongList
                .SelectMany(song => song.Constants.Select((constant, i) => (constant, i, song)))
                .Where(t => levelIdxes.Contains(t.i))
                .Where(t => q.Selectors.All(sel => MatchSelector(sel, t.constant, t.i, t.song, includeRevival)))
                .Select(t => (t.constant, t.i, t.song))
                .ToList();
        }

        // 单 chart × 单 selector 的命中判断；handler 用 Selectors.All(...) 求 AND 交集。
        static bool MatchSelector(PlateData.Selector sel, double constant, int levelIdx, MaiMaiSong song, bool includeRevival) => sel switch
        {
            PlateData.Selector.Plate p => PlateData.MatchPlate(p, song, levelIdx, includeRevival),

            // 复活曲集合（虚拟类别）。
            PlateData.Selector.Revival => PlateData.IsRevivalSong(song.Id),

            PlateData.Selector.Charter c =>
                levelIdx < song.Charters.Count
                && PlateData.MatchCharter(song.Charters[levelIdx], c.Name),

            PlateData.Selector.CharterAlias ca =>
                levelIdx < song.Charters.Count
                && PlateData.MatchCharter(song.Charters[levelIdx], ca.Names, ca.Exclude),

            // song-level substring 匹配，兼容 "sasakure.UK x DECO*27" 这种合作作曲名义。
            PlateData.Selector.Artist a =>
                !string.IsNullOrEmpty(song.Info.Artist)
                && song.Info.Artist.Contains(a.Name, StringComparison.OrdinalIgnoreCase),

            // 谱师 ∪ 作曲家：处理 "rintaro soma" 这种身兼两职的人。
            PlateData.Selector.CharterOrArtist ca =>
                (levelIdx < song.Charters.Count
                 && PlateData.MatchCharter(song.Charters[levelIdx], ca.Name))
                || (!string.IsNullOrEmpty(song.Info.Artist)
                    && song.Info.Artist.Contains(ca.Name, StringComparison.OrdinalIgnoreCase)),

            PlateData.Selector.Genre g =>
                string.Equals(song.Info.Genre, g.FullName, StringComparison.Ordinal),

            // 难度 label：匹 song.Levels[i] 精确相等
            PlateData.Selector.Level lvl =>
                levelIdx < song.Levels.Count
                && string.Equals(song.Levels[levelIdx], lvl.Label, StringComparison.Ordinal),

            // 定数：song.Constants[i] 精确等于 (0.05 tolerance for floating point safety；定数小数点 1 位)
            PlateData.Selector.Constant cst =>
                Math.Abs(constant - cst.Value) < 0.05,

            _ => false,
        };
    }

    #endregion

    #region 打什么歌

    [MarisaPluginDoc("如何**推分**到目标")]
    [MarisaPluginCommand("howto", "how to")]
    private async Task<MarisaPluginTaskState> HowTo(Message message)
    {
        if (!int.TryParse(message.Command.Span, out var target))
        {
            message.Reply("参数不是数字");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fetcher = GetDataFetcher(message);
        var rating = await fetcher.GetRating(message with
        {
            Command = "".AsMemory()
        });

        var (old, @new, success) = GetRecommend(rating, target);

        if (!success)
        {
            message.Reply("no way");
            return MarisaPluginTaskState.CompletedTask;
        }

        var current = new
        {
            OldScores = rating.OldScores
                .Select(x => (Song: SongDb.GetSongById(x.Id), x.LevelIdx, x.Achievement, x.Rating))
                .Where(x => x.Song != null)
                .Select(x => (x.Song!, x.LevelIdx, x.Achievement, x.Rating))
                .OrderByDescending(x => x.Item4),
            NewScores = rating.NewScores
                .Select(x => (Song: SongDb.GetSongById(x.Id), x.LevelIdx, x.Achievement, x.Rating))
                .Where(x => x.Song != null)
                .Select(x => (x.Song!, x.LevelIdx, x.Achievement, x.Rating))
                .OrderByDescending(x => x.Item4)
        };

        var recommend = new
        {
            OldScores = old.OrderByDescending(x => x.Item4),
            NewScores = @new.OrderByDescending(x => x.Item4)
        };

        var context = new WebContext();

        context.Put("current", current);
        context.Put("recommend", recommend);

        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiRecommend(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     mai什么
    /// </summary>
    [MarisaPluginDoc("随机给出一个歌")]
    [MarisaPluginCommand("打什么歌", "打什么", "什么")]
    private MarisaPluginTaskState PlayWhat(Message message)
    {
        message.Reply(MessageDataImage.FromBase64(SongDb.SongList.RandomTake().GetImage()));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     mai什么推分
    /// </summary>
    [MarisaPluginDoc("随机给出至多 4 首打了以后能推分的歌")]
    [MarisaPluginSubCommand(nameof(PlayWhat))]
    [MarisaPluginCommand(true, "推分", "恰分", "上分", "加分")]
    private async Task<MarisaPluginTaskState> PlayWhatToUp(Message message)
    {
        var fetcher   = GetDataFetcher(message);
        var rating    = await fetcher.GetRating(message);
        var recommend = rating.DrawRecommendCard(SongDb.SongList);

        if (recommend == null)
        {
            message.Reply("您无分可恰");
        }
        else
        {
            message.Reply(MessageDataImage.FromBase64(recommend.ToB64()));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 分数线 / 容错率

    /// <summary>
    ///     分数线，达到某个达成率rating会上升的线
    /// </summary>
    [MarisaPluginDoc("给出定数对应的所有 rating 或 rating 对应的所有定数", "`歌曲定数` 或 `预期rating`")]
    [MarisaPluginCommand("line", "分数线")]
    private static MarisaPluginTaskState RatingLine(Message message)
    {
        var command = message.Command.Trim().ToString();

        // 定数分支走严格解析（一位小数，拒符号/千分位/NaN），预期 rating 分支照旧
        if (TryParseConstant(command, out var constant))
        {
            var a   = 96.9999;
            var ret = "达成率 -> Rating";

            while (a < 100.5)
            {
                a = SongScore.NextRa(a, constant);
                var ra = SongScore.Ra(a, constant);
                ret = $"{ret}\n{a:000.0000} -> {ra}";
            }

            message.Reply(ret);
            return MarisaPluginTaskState.CompletedTask;
        }

        if (double.TryParse(command, out var expected))
        {
            switch (expected)
            {
                case > 15:
                {
                    var result = new List<(double Constant, double Achievement)>();
                    var ret    = "定数 -> 达成率 -> rating\n";

                    Enumerable.Range(1, 150)
                        .Where(rat =>
                            SongScore.Ra(100.5, rat / 10.0) >= expected && SongScore.Ra(50, rat / 10.0) <= expected)
                        .ToList()
                        .ForEach(rat =>
                        {
                            var a = 49.0;
                            while (a < 100.5)
                            {
                                a = SongScore.NextRa(a, rat / 10.0);
                                var ra = SongScore.Ra(a, rat / 10.0);

                                if (ra != (int)expected) continue;

                                result.Add((rat / 10.0, a));
                                break;
                            }
                        });

                    ret += string.Join('\n',
                        result.Select(x => $"{x.Constant:00.0} -> {x.Achievement:000.0000} -> {(int)expected}"));

                    message.Reply(ret);
                    return MarisaPluginTaskState.CompletedTask;
                }
            }
        }

        message.Reply("参数应为“定数”");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("计算某首歌曲的容错率", "`歌名`")]
    [MarisaPluginCommand("tolerance", "tol", "容错率")]
    private async Task<MarisaPluginTaskState> FaultTolerance(Message message)
    {
        var songName     = message.Command.Trim();
        var searchResult = SongDb.SearchSong(songName);

        var song = await SongDb.MultiPageSelectResult(searchResult, message, false, true);
        if (song == null)
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("难度和预期达成率？");
        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var command = next.Command.Trim();

            if (!PlateData.TryStripDifficultyPrefixLoose(command, out var levelIdx, out var rest))
            {
                next.Reply("错误的难度格式，会话已关闭。可用难度格式：难度全名、缩写、颜色或全名首字母");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var parseSuccess = double.TryParse(rest.Span, out var achievement);

            if (!parseSuccess)
            {
                next.Reply("错误的达成率格式，会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (achievement is > 101 or < 0)
            {
                next.Reply("你查**呢");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (levelIdx >= song.Charts.Count)
            {
                next.Reply("该谱面没有这个难度，会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }
            var (x, y) = song.NoteScore(levelIdx);

            var tolerance = (int)((101 - achievement) / (0.2 * x));
            var dxScore   = song.Charts[levelIdx].Notes.Sum() * 3;

            var dxScores = new[]
                {
                    0.85, 0.9, 0.93, 0.95, 0.97
                }
                .Select(mul => ((int)Math.Ceiling(dxScore * mul), dxScore - (int)Math.Ceiling(dxScore * mul)))
                .ToArray();

            next.Reply(
                new MessageDataText($"[{MaiMaiSong.LevelNameAll[levelIdx]}] {song.Title} => {achievement:F4}\n"),
                new MessageDataText($"至多粉 {tolerance} 个 TAP，每个减 {0.2 * x:F4}%\n"),
                new MessageDataText($"绝赞 50 落相当于粉 {0.25 * y / (0.2 * x):F4} 个 TAP，每 50 落减 {0.25 * y:F4}%\n"),
                new MessageDataText($"\nDX分：{dxScore}\n"),
                new MessageDataText($"★ 最低 {dxScores[0].Item1}(-{dxScores[0].Item2})\n"),
                new MessageDataText($"★★ 最低 {dxScores[1].Item1}(-{dxScores[1].Item2})\n"),
                new MessageDataText($"★★★ 最低 {dxScores[2].Item1}(-{dxScores[2].Item2})\n"),
                new MessageDataText($"★★★★ 最低 {dxScores[3].Item1}(-{dxScores[3].Item2})\n"),
                new MessageDataText($"★★★★★ 最低 {dxScores[4].Item1}(-{dxScores[4].Item2})\n"),
                new MessageDataText("每小DX分减1，每粉DX分减2，否则DX分减3\n"),
                MessageDataImage.FromBase64(MaiMaiDraw.DrawFaultTable(x, y).ToB64())
            );
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }, this);


        return MarisaPluginTaskState.CompletedTask;
    }


    #endregion
}
