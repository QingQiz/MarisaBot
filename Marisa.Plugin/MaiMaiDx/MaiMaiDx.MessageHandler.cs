using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
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
    private Task<MarisaPluginTaskState> Bind(Message message, TimeProvider timeProvider)
    {
        var servers = new[]
        {
            "DivingFish", "lxns",
        };

        message.Reply("请选择查分器（序号）：\n\n" + string.Join('\n', servers
            .Select((x, i) => (x, i))
            .Select(x => $"{x.i}. {x.x}"))
        );

        var stat = 0;
        string? oauthVerifier = null;
        DivingFishDeviceBindingSession? deviceBinding = null;

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), HandleBindingMessage, this);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);

        MarisaPluginTaskState DoBind(Message msg, string srv)
        {
            using var realm = BotDbContext.OpenRealm();
            var tr = realm.BeginWrite();

            var bind = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == msg.Sender.Id);
            if (bind == null)
                realm.AddWithAutoId(new MaiMaiDxBind(msg.Sender.Id, 0) { ServerName = srv });
            else
                bind.ServerName = srv;

            tr.Commit();
            return MarisaPluginTaskState.CompletedTask;
        }

        async Task<MarisaPluginTaskState> HandleBindingMessage(Message next)
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

                    if (idx == 0 && DivingFishOAuth.IsConfigured)
                    {
                        try
                        {
                            if (await GetDataFetcher(DataFetcherType.DivingFish).TestOAuthToken(next.Sender.Id))
                            {
                                next.Reply("DivingFish OAuth 绑定成功！（已有有效授权）");
                                return DoBind(next, servers[idx]);
                            }
                        }
                        catch (Exception e)
                        {
                            next.Reply($"水鱼 OAuth 暂不可用：{e.Message}");
                            return MarisaPluginTaskState.CompletedTask;
                        }

                        if (DivingFishOAuth.CanUseDeviceCode)
                        {
                            var device = await DivingFishOAuth.StartDeviceAuthorization(
                                "maimai",
                                DeviceBindingLabel(next.Sender.Id));
                            next.Reply(MessageChain.FromSensitiveText(
                                $"请打开水鱼授权链接完成绑定（{device.ExpiresIn / 60} 分钟内有效）：\n{device.VerificationUriComplete}\n\n用户码：{device.UserCode}"));

                            stat = 30;
                            deviceBinding = new DivingFishDeviceBindingSession(message, "maimai", HandleBindingMessage, timeProvider,
                                qq => GetDataFetcher(DataFetcherType.DivingFish).TestOAuthToken(qq));
                            _ = deviceBinding.RunAsync(device);
                            return MarisaPluginTaskState.ToBeContinued;
                        }
                    }

                    if (idx == 1 && !string.IsNullOrWhiteSpace(ConfigurationManager.Configuration.Lxns.Oauth.ClientId))
                    {
                        // 已有有效 Token → 跳过 OAuth
                        try
                        {
                            if (await GetDataFetcher(DataFetcherType.Lxns).TestOAuthToken(next.Sender.Id))
                            {
                                message.Reply("Lxns OAuth 绑定成功！(已授权，跳过认证)");
                                return DoBind(next, servers[idx]);
                            }
                        }
                        catch (Exception e)
                        {
                            message.Reply($"Lxns OAuth 暂不可用：{e.Message}");
                            return MarisaPluginTaskState.CompletedTask;
                        }

                        // lxns OAuth 流程：只做 token 获取，绑定写入通过状态机 fall through
                        var (verifier, challenge) = LxnsOAuth.GeneratePkcePair();
                        var state = Guid.NewGuid().ToString("N")[..8];
                        var url = LxnsOAuth.GetAuthorizationUrl(challenge, state);
                        var shortCode = ShortUrlStore.CreateShortUrl(url);
                        var shortUrl = ShortUrlStore.GetShortUrl(shortCode);

                        message.Reply(MessageChain.FromSensitiveText(
                            $"请打开以下链接授权：\n{shortUrl}\n\n授权成功后复制并发送显示的验证码（形如XXXX-XXXX-XXXX）"));

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
                    if (!Regex.IsMatch(codeInput, "^[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}$"))
                    {
                        next.Reply("验证码格式错误，会话已关闭");
                        return MarisaPluginTaskState.CompletedTask;
                    }

                    try
                    {
                        var token = await LxnsOAuth.ExchangeCode(next.Command.Trim().ToString(), oauthVerifier!);
                        LxnsTokenStore.SaveToken(next.Sender.Id, token.AccessToken, token.RefreshToken,
                            (int)(token.ExpiresAt - DateTime.UtcNow).TotalSeconds);

                        // 授权刚完成也要实测一次：令牌能被玩家接口接受才算绑定成功
                        if (!await GetDataFetcher(DataFetcherType.Lxns).TestOAuthToken(next.Sender.Id))
                        {
                            next.Reply("Lxns OAuth 授权已完成，但令牌验证未通过，请重新授权");
                            return MarisaPluginTaskState.CompletedTask;
                        }

                        message.Reply("Lxns OAuth 绑定成功！");
                        return DoBind(next, "lxns");
                    }
                    catch (Exception e)
                    {
                        next.Reply($"绑定失败: {e.Message}");
                        return MarisaPluginTaskState.CompletedTask;
                    }
                }
                case 30:
                    return await deviceBinding!.Confirm(next);
            }

            return MarisaPluginTaskState.CompletedTask;
        }
    }

    #endregion

    #region 推分同步（导）

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
        const string usageText =
            "用法：\n" +
            "mai 导 —— 同步成绩到查分器（首次使用会引导设置）\n" +
            "mai 导 <好友码> —— 绑定/换绑好友码\n" +
            "mai 导 落雪 xxx 水鱼 yyy —— 设置查分器导入令牌（可只填其中一个）\n" +
            "令牌获取方法：\n\n" +
            "水鱼：首页-编辑个人资料-成绩导入Token\n\n" +
            "落雪：账号详情-个人API密钥\n\n" +
            "建议发送令牌后立即「撤回」消息。";

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
            message.Reply($"有解析失败的参数（请确认令牌前后有空格分隔）。\n{usageText}");
            return MarisaPluginTaskState.CompletedTask;
        }

        var newTokens = lxns == null && df == null ? ((string? Lxns, string? DivingFish)?)null : (lxns, df);

        // 同步任务可能持续数分钟，期间拒绝新的指令；携带令牌的指令需明确告知未提交，避免用户误以为设置已生效
        if (Syncing.ContainsKey(qq))
        {
            message.Reply(newTokens == null
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
            StartSync(message, friendCode, newTokens);
            return MarisaPluginTaskState.CompletedTask;
        }

        // 首次使用：通过对话引导完成设置，先收集好友码，必要时再收集令牌。
        // 实现为单个对话的状态机（ToBeContinued 表示继续当前对话）。注意不能在对话 handler 内
        // 对同一 key 再次调用 AddDialogAsync：它会自旋等待 key 释放，而 key 要到 handler 返回
        // 之后才会释放，二者互相等待形成死锁。
        message.Reply(newTokens == null
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
                    next.Reply("好友码应为 15 位数字，解析失败，已退出设置。可重新发送「mai 导」。");
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

                next.Reply(
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
                next.Reply("没有解析到令牌（格式：落雪 xxx / 水鱼 yyy），已退出设置。之后可随时发送「mai 导 落雪 xxx 水鱼 yyy」完成设置。");
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            if (leftover != null || fcExtra != null)
            {
                // 部分解析成功（如「水鱼yyy」缺少空格）时整体拒绝，避免用户误以为两个令牌都已设置；
                // 原文不回显，其中可能包含真实令牌
                next.Reply("部分参数解析失败（请确认令牌前后有空格分隔），已退出设置。可重新发送「mai 导 落雪 xxx 水鱼 yyy」。" +
                           (next.GroupInfo != null ? "建议立即「撤回」含有令牌的消息。" : string.Empty));
                return Task.FromResult(MarisaPluginTaskState.Canceled);
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
            var t = realm.BeginWrite();
            var bind = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == uid);
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

            t.Commit();
        }
    }

    private static void StartSync(Message message, string friendCode, (string? Lxns, string? DivingFish)? newTokens)
    {
        if (!Syncing.TryAdd(message.Sender.Id, 0))
        {
            message.Reply(newTokens == null
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
                message.Reply($"同步失败：{e.Message}。{RetryHint(newTokens)}");
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

        message.Reply("少女祈祷中...");

        var login = await CreateLoginRequest();
        var announced = false;
        if (login.FriendRequestSent && !string.IsNullOrEmpty(login.BotFriendCode))
        {
            message.Reply($"已发出好友申请（好友码{login.BotFriendCode}），请尽快到 NET 同意");
            announced = true;
        }

        MaiScoreHubClient.LoginStatusResult? status = null;
        var waitStart = DateTime.UtcNow;
        var deadline = login.DeadlineAt?.UtcDateTime.AddMinutes(1) ?? waitStart.AddMinutes(15);
        var pollFailures = 0;
        var sawAcceptance = login.FriendRequestSent;
        var queuedNotified = false;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollDelayMs(DateTime.UtcNow - waitStart));

            try
            {
                status = await msh.LoginStatusAsync(login.JobId);
                pollFailures = 0;
            }
            catch (Exception e)
            {
                if (++pollFailures < 6) continue;

                message.Reply($"同步中断：连续多次查询任务状态失败（{e.Message}）。稍后{retryHint}");
                return;
            }

            if (status.FriendRequestSent) sawAcceptance = true;

            if (status.Done || !string.IsNullOrEmpty(status.Token)) break;

            if (status.Status is "failed" or "canceled")
            {
                if (status.DeadlineExceeded)
                {
                    message.Reply(sawAcceptance
                        ? $"同步失败：MSH 好友验证任务已超过服务端截止时间，好友申请虽已发出，但未能及时确认好友关系。稍后{retryHint}"
                        : $"同步失败：MSH 未能在服务端截止前发出好友申请（服务排队超时，与你是否同意无关）。稍后{retryHint}");
                }
                else
                {
                    message.Reply($"同步失败：{status.Message ?? status.Status}。{retryHint}");
                }

                return;
            }

            if (!announced && status.Stage == "wait_acceptance" && !string.IsNullOrEmpty(status.BotFriendCode))
            {
                message.Reply($"已发出好友申请（好友码{login.BotFriendCode}），请尽快到 NET 同意");
                announced = true;
            }

            // 老用户（已是好友）不会触发好友申请提示，任务也常卡在 send_request 排队；等待超过 30 秒仍无进展时
            // 一次性告知请求已被受理，避免整段静默后只见超时
            if (!announced && !queuedNotified && DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(30))
            {
                message.Reply("少女祈祷中...");
                queuedNotified = true;
            }
        }

        if (status == null || (!status.Done && string.IsNullOrEmpty(status.Token)))
        {
            message.Reply(sawAcceptance
                ? $"等待超时（可能未及时同意好友申请）。同意后{retryHint}"
                : $"等待超时（服务繁忙，好友申请仍在排队、未能及时处理，与你是否同意无关）。稍后{retryHint}");
            return;
        }

        // JWT 随登录任务完成（好友关系确认）下发；创建登录任务时的 authToken 仅作回退
        var jwt = !string.IsNullOrEmpty(status.Token) ? status.Token! : login.AuthToken;
        if (string.IsNullOrEmpty(jwt))
        {
            message.Reply("登录完成，但未获取到登录凭据（MSH 的 token 下发方式可能已变更）。请向开发者反馈。");
            return;
        }

        // 一拿到 JWT 即把令牌存入 MSH：抓分等待可能超时，提前提交可避免本次令牌白费、被迫重发令牌
        if (newTokens is { } t)
        {
            if (!string.IsNullOrWhiteSpace(t.Lxns)) await msh.SetTokenAsync(jwt, "lxns", t.Lxns!);
            if (!string.IsNullOrWhiteSpace(t.DivingFish)) await msh.SetTokenAsync(jwt, "diving-fish", t.DivingFish!);
            retryHint = "重试「mai 导」即可。"; // 令牌已存入 MSH，后续失败无需再带令牌重发
        }

        // 第二阶段：创建独立的抓分任务并等待完成。登录任务不再包含抓分，把登录任务号作为
        // 好友关系凭证传入可立即开始抓分。抓分阶段单独计时：第一阶段需等待好友申请送达并被
        // 接受，繁忙时可能已耗去大部分时间，共用截止时间会使抓分预算所剩无几
        MaiScoreHubClient.JobStartResult crawl;
        try
        {
            crawl = await msh.CreateUpdateScoreJobAsync(jwt, login.JobId);
        }
        catch (Exception e)
        {
            message.Reply($"同步失败：创建抓分任务未成功（{e.Message}）。{retryHint}");
            return;
        }

        var crawlJobId = crawl.JobId;
        deadline = crawl.DeadlineAt?.UtcDateTime.AddMinutes(1) ?? DateTime.UtcNow.AddMinutes(20);
        var crawlStart = DateTime.UtcNow;

        var crawlDone = false;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollDelayMs(DateTime.UtcNow - crawlStart));

            MaiScoreHubClient.JobResult job;
            try
            {
                job = await msh.GetJobAsync(jwt, crawlJobId);
                pollFailures = 0;
            }
            catch (Exception e)
            {
                if (++pollFailures < 6) continue;

                message.Reply($"同步中断：连续多次查询任务状态失败（{e.Message}）。稍后{retryHint}");
                return;
            }

            if (job.Status == "completed")
            {
                crawlDone = true;
                break;
            }

            if (job.Status is "failed" or "canceled")
            {
                message.Reply(job.DeadlineExceeded
                    ? $"同步失败：MSH 未能在服务端截止前完成成绩抓取，本次任务已结束。稍后{retryHint}"
                    : $"同步失败：{job.Error ?? job.Status}。{retryHint}");
                return;
            }
        }

        if (!crawlDone)
        {
            message.Reply($"等待超时（服务繁忙，成绩抓取未在限定时间内完成）。稍后{retryHint}");
            return;
        }

        // 查询 MSH 中已配置的查分器
        var profile = await msh.GetProfileAsync(jwt);

        var targets = new List<string>();
        if (profile.HasLxns) targets.Add("lxns");
        if (profile.HasDivingFish) targets.Add("diving-fish");

        if (targets.Count == 0)
        {
            message.Reply("成绩已抓取，但尚未配置任何查分器令牌。发送「mai 导 落雪 xxx 水鱼 yyy」完成设置（可只填其中一个，建议发送令牌后立即「撤回」消息）。");
            return;
        }

        var sb = new StringBuilder("同步完成：\n");
        foreach (var p in targets)
        {
            var name = p == "lxns" ? "落雪" : "水鱼";
            try
            {
                // 导出为异步任务，ExportAsync 内部轮询至终态后返回该查分器的回执
                var r = await msh.ExportAsync(jwt, p);

                sb.AppendLine(r.Success ? $"{name} ✅ 导入 {r.Exported}/{r.Scores} 条" : $"{name} ❌ {r.Message ?? "失败"}");
            }
            catch (Exception e)
            {
                sb.AppendLine($"{name} ❌ {e.Message}");
            }
        }

        message.Reply(sb.ToString().TrimEnd());

        async Task<MaiScoreHubClient.LoginRequestResult> CreateLoginRequest()
        {
            for (var attempt = 1;; attempt++)
            {
                try
                {
                    return await msh.LoginRequestAsync(friendCode);
                }
                catch (MaiScoreHubApiException e) when (e.IsTransientLoginFailure && attempt < 4)
                {
                    await Task.Delay(5000);
                }
            }
        }
    }

    #endregion

    #region 查分

    /// <summary>
    ///     b35
    /// </summary>
    [MarisaPluginDoc("查询 b35，不论新旧版本", "`查分器的账号名` 或 `@某人` 或 `留空`")]
    [MarisaPluginCommand("b35")]
    private async Task<MarisaPluginTaskState> B35(Message message)
    {
        var player = ResolvePlayer(message, allowUsername: true);
        var (nickname, all) = await player.Fetcher.GetScores(player);

        // b35 不分新旧版本：全部成绩按 rating 排序取前 35 + 之后 15
        var scores = all
            .Where(kv => kv.Key.Id <= 100000)
            .OrderByDescending(kv => kv.Value.Rating).ThenBy(x => x.Key.Id)
            .Select(x => x.Value)
            .ToList();

        var b35 = new DxRating
        {
            Nickname = nickname,
            OldScores = scores.Take(DivingFishDataFetcher.OldScoreLimit).ToList(),
            NewScores = scores.Skip(DivingFishDataFetcher.OldScoreLimit).Take(DivingFishDataFetcher.NewScoreLimit).ToList()
        };

        var context = new WebContext(new { b50 = b35 });

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
        var player = ResolvePlayer(message, allowUsername: true);

        var b50 = await player.Fetcher.GetRating(player);

        var context = new WebContext();

        context.Put("b50", b50);

        message.Reply(MessageChain.FromImageB64(await WebApi.MaiMaiBest(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("分析拟合难度高于官方定数的谱面；支持按评级、等级、谱面难度和官方定数筛选",
        "无筛选时分析 B50；筛选置于命令前并取交集，如`鸟加 14+ 紫谱 14.8`")]
    [MarisaPluginCommand(true, "含金量分析")]
    private Task<MarisaPluginTaskState> GoldValueAnalysis(Message message)
    {
        return ValueAnalysis(message, MaiValueAnalysisMode.Gold, MaiValueAnalysisFilter.Empty);
    }

    [MarisaPluginDoc("分析拟合难度低于官方定数的谱面；支持按评级、等级、谱面难度和官方定数筛选",
        "无筛选时分析 B50；筛选置于命令前并取交集，如`鸟加 14+ 紫谱 14.8`")]
    [MarisaPluginCommand(true, "水分分析")]
    private Task<MarisaPluginTaskState> WaterValueAnalysis(Message message)
    {
        return ValueAnalysis(message, MaiValueAnalysisMode.Water, MaiValueAnalysisFilter.Empty);
    }

    [MarisaPluginNoDoc]
    [MarisaPluginTrigger(typeof(MaiMaiDx), nameof(FilteredValueAnalysisTrigger),
        MessageType.GroupMessage | MessageType.FriendMessage | MessageType.TempMessage)]
    private async Task<MarisaPluginTaskState> FilteredValueAnalysis(Message message)
    {
        if (!TryParseValueAnalysisCommand(message.Command, out var mode, out var filter) || filter.IsEmpty)
        {
            return MarisaPluginTaskState.NoResponse;
        }

        return await ValueAnalysis(message, mode, filter);
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

    [MarisaPluginDoc("比较双方单曲成绩；不填歌曲时随机选择一首共同已玩谱面", "`@某人`，可选歌曲名、别名、ID 或难度")]
    [MarisaPluginCommand("vs", "对战")]
    private async Task<MarisaPluginTaskState> SongVersus(Message message)
    {
        if (ResolveVersusPlayers(message) is not { } players) return MarisaPluginTaskState.CompletedTask;

        var query     = message.Command.Trim().ToString();
        var selection = ResolveVersusQuery(SongDb, query);
        if (selection.Scope is not null)
        {
            message.Reply("完成表范围请使用 mai vs b 范围 @对手");
            return MarisaPluginTaskState.CompletedTask;
        }

        var levelIdx = selection.LevelIndex;

        // 指定歌曲时先完成选歌（可能要等用户选择），再取成绩
        MaiMaiSong? song = null;
        if (!selection.Random)
        {
            song = await SongDb.MultiPageSelectResult(selection.Songs, message, false, true);
            if (song is null) return MarisaPluginTaskState.CompletedTask;

            if (levelIdx >= song.Levels.Count)
            {
                message.Reply($"该歌曲没有{MaiMaiSong.LevelNameZh[levelIdx]}谱");
                return MarisaPluginTaskState.CompletedTask;
            }
        }

        if (await FetchVersusScores(message, players) is not { } sides) return MarisaPluginTaskState.CompletedTask;

        var (self, opponent) = sides;

        if (song is null)
        {
            var candidates = SharedVersusSongs(SongDb.SongList, levelIdx, self.Scores, opponent.Scores);
            if (candidates.Count == 0)
            {
                message.Reply("没有找到双方都已游玩且当前可查询的谱面");
                return MarisaPluginTaskState.CompletedTask;
            }

            song = candidates[Random.Shared.Next(candidates.Count)];
        }

        var selfScore     = self.Scores.GetValueOrDefault((song.Id, levelIdx));
        var opponentScore = opponent.Scores.GetValueOrDefault((song.Id, levelIdx));

        // 只按达成率判定胜负：0 = 自己，1 = 对手，-1 = 平局或双方均未游玩
        var winnerIndex = selfScore == null && opponentScore == null ? -1
            : selfScore == null ? 1
            : opponentScore == null ? 0
            : selfScore.Achievement == opponentScore.Achievement ? -1
            : selfScore.Achievement > opponentScore.Achievement ? 0 : 1;

        var context = new WebContext(new
        {
            versus = new
            {
                Song = new { song.Id, song.Title, song.Type, song.Info.Artist, song.Info.Genre, song.Info.Bpm, song.Info.From, song.Info.IsNew },
                LevelIndex = levelIdx,
                Level = song.Levels[levelIdx],
                Constant = song.Constants[levelIdx],
                MaxDx = song.Charts[levelIdx].Notes.Sum() * 3,
                Players = new[]
                {
                    new { Nickname = self.Name, Played = selfScore != null, Score = ProjectScore(selfScore) },
                    new { Nickname = opponent.Name, Played = opponentScore != null, Score = ProjectScore(opponentScore) }
                },
                WinnerIndex = winnerIndex
            }
        });

        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiVersus(context.Id)));
        return MarisaPluginTaskState.CompletedTask;

        object? ProjectScore(SongScore? score)
        {
            return score == null
                ? null
                : new
                {
                    score.Achievement,
                    Rank = SongScore.CalcRank(score.Achievement),
                    Rating = song.Ra(levelIdx, score.Achievement),
                    score.DxScore,
                    score.Fc,
                    score.Fs,
                };
        }
    }

    [MarisaPluginDoc("从双方共同已玩谱面中随机抽取若干首比较", "`数量`（1～20），可选歌曲、难度或完成表范围")]
    [MarisaPluginSubCommand(nameof(SongVersus))]
    [MarisaPluginTrigger(typeof(MaiMaiDx), nameof(VersusRandomTrigger))]
    [MarisaPluginCommand("n")]
    private async Task<MarisaPluginTaskState> SongVersusRandom(Message message)
    {
        var args = message.Command.Trim().ToString().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0 || !int.TryParse(args[0], out var count) || count is < 1 or > MaxVersusRandomCount)
        {
            message.Reply($"随机对战数量必须是 1～{MaxVersusRandomCount} 的整数，用法：mai vs n 5 [歌曲/难度/范围] @对手");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (ResolveVersusPlayers(message) is not { } players) return MarisaPluginTaskState.CompletedTask;

        var query     = args.Length > 1 ? args[1] : string.Empty;
        var selection = ResolveVersusQuery(SongDb, query);
        if (selection is { Random: false, Scope: null, Songs.Count: 0 })
        {
            message.Reply($"没有找到 {query} 对应的歌曲或范围");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (await FetchVersusScores(message, players) is not { } sides) return MarisaPluginTaskState.CompletedTask;

        var (self, opponent) = sides;

        var levelIdx = selection.LevelIndex;
        var shared = selection.Scope is null
            ? SharedVersusSongs(selection.Random ? SongDb.SongList : selection.Songs, levelIdx, self.Scores, opponent.Scores)
                .Select(song => (Constant: song.Constants[levelIdx], LevelIdx: levelIdx, Song: song))
                .ToList()
            : PlateData.SelectScopeCharts(selection.Scope, SongDb.SongList)
                .Where(x => self.Scores.ContainsKey((x.Song.Id, x.LevelIdx)) && opponent.Scores.ContainsKey((x.Song.Id, x.LevelIdx)))
                .ToList();

        if (shared.Count < count)
        {
            message.Reply($"双方共同游玩且当前可查询的谱面只有 {shared.Count} 首，无法随机选择 {count} 首");
            return MarisaPluginTaskState.CompletedTask;
        }

        var charts = shared.OrderBy(_ => Random.Shared.Next()).Take(count).ToArray();
        await ReplyBatchVersus(message, new MaiVersusBatch($"随机 {count} 首共同谱面", string.Empty, "随机", charts, self, opponent));
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("比较完成表范围内的全部谱面", "`完成表范围`，如`彩代14+`")]
    [MarisaPluginSubCommand(nameof(SongVersus))]
    [MarisaPluginTrigger(typeof(MaiMaiDx), nameof(VersusBatchTrigger))]
    [MarisaPluginCommand("b")]
    private async Task<MarisaPluginTaskState> SongVersusBatch(Message message)
    {
        var query = message.Command.Trim().ToString();
        if (!PlateData.TryParseScope(query, out var scope, out _))
        {
            message.Reply($"无法解析完成表范围：{query}");
            return MarisaPluginTaskState.CompletedTask;
        }

        // 先在本地确认范围内有谱面，免得为无效范围白取一次成绩
        var charts = PlateData.SelectScopeCharts(scope, SongDb.SongList);
        if (charts.Count == 0)
        {
            message.Reply($"没有找到 {query} 对应的谱面");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (ResolveVersusPlayers(message) is not { } players) return MarisaPluginTaskState.CompletedTask;
        if (await FetchVersusScores(message, players) is not { } sides) return MarisaPluginTaskState.CompletedTask;

        var (self, opponent) = sides;

        var versions  = charts.Select(x => x.Song.Version).Distinct().ToArray();
        var sortLabel = scope.Selectors.Any(x => x is PlateData.Selector.Constant or PlateData.Selector.ConstantRange)
            ? "歌曲 ID 升序"
            : "定数降序";

        await ReplyBatchVersus(message,
            new MaiVersusBatch(query, versions.Length == 1 ? versions[0] : string.Empty, sortLabel, charts, self, opponent));
        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>取一方的 vs 数据；授权/网络错误转成 Error 文本交给调用方回复，其余异常照旧抛出。</summary>
    private static async Task<BattleData> FetchBattleData(ResolvedPlayer player)
    {
        try
        {
            var (nickname, scores) = await player.Fetcher.GetScores(player);
            return new BattleData(nickname, scores, null);
        }
        catch (HttpRequestException e)
        {
            return new BattleData(null, [], e.Message);
        }
    }

    private sealed record BattleData(
        string? Nickname,
        Dictionary<(long Id, int LevelIdx), SongScore> Scores,
        string? Error);

    /// <summary>
    ///     谱面预览
    /// </summary>
    [MarisaPluginDoc("谱面预览，回复在线播放页链接", "可选难度（如`白谱`，曲名前后皆可）+ `歌曲名` 或 `歌曲别名` 或 `歌曲id`")]
    [MarisaPluginCommand("chart", "谱面", "预览", "preview")]
    private async Task<MarisaPluginTaskState> SongChartPreview(Message message)
    {
        var command = message.Command.Trim();

        var searchResult = SongDb.SearchSong(command);

        int? levelIdx = null;
        if (searchResult.Count == 0 && PlateData.TryStripDifficultyAffix(command, out var idx, out var rest))
        {
            var stripped = SongDb.SearchSong(rest);
            if (stripped.Count != 0)
            {
                levelIdx = idx;
                searchResult = stripped;
            }
        }

        var song = await SongDb.MultiPageSelectResult(searchResult, message, false, true);
        if (song == null) return MarisaPluginTaskState.CompletedTask;

        if (levelIdx >= song.Levels.Count)
        {
            message.Reply($"该谱面没有 {MaiMaiSong.LevelNameAll[levelIdx.Value]} 难度");
            return MarisaPluginTaskState.CompletedTask;
        }

        var url = $"{ShortUrlStore.GetPublicBaseUrl()}/maimai/chart?id={song.Id}" +
                  (levelIdx == null ? string.Empty : $"&difficulty={levelIdx}");
        message.Reply($"[{song.Type}] {song.Title}\n{url}");

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
                levelIdx = idx;
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
        // 只取这一首歌各难度的成绩：各查分器优先走自己的「单曲成绩接口」，避免拉取整个成绩表
        var player = ResolvePlayer(message);
        var (nickname, scores) = await player.Fetcher.GetSongScore(player, song);

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
                Nickname = nickname ?? string.Empty
            },
            Charts = song.Levels.Select((level, i) =>
            {
                var played = scores.TryGetValue(i, out var sc);
                return new
                {
                    LevelIndex = i,
                    Level = level,
                    Constant = song.Constants[i],
                    Charter = song.Charters[i],
                    MaxDx = song.Charts[i].Notes.Sum() * 3,
                    Played = played,
                    Achievement = played ? sc!.Achievement : (double?)null,
                    Rank = played ? sc!.Rank : null,
                    Ra = played ? sc!.Rating : (int?)null,
                    Fc = played ? sc!.Fc : null,
                    Fs = played ? sc!.Fs : null,
                    DxScore = played ? sc!.DxScore : (int?)null
                };
            }).ToList()
        });

        return context;
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
        var player = ResolvePlayer(message);

        // 旧谱的操作和新谱的一样，所以直接复制了，为这两个抽象一层有点不值
        var groupedSong = SongDb.SongList
            .Where(song => song.Info.IsNew)
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.i >= 2)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.song.Levels[x.i]);

        var (_, scores) = await player.Fetcher.GetScores(player);

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

            var player = ResolvePlayer(message);
            var (_, scores) = await player.Fetcher.GetScores(player);

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
            var player = ResolvePlayer(message);
            var (_, scores) = await player.Fetcher.GetScores(player);

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

        var versionArg = message.Command.Trim().ToString();
        if (versionArg.Length > 0)
        {
            var version = ResolveSummaryVersion(versionArg, versions);
            if (version == null)
            {
                message.Reply("错误的版本：" + versionArg);
                return MarisaPluginTaskState.CompletedTask;
            }

            await ReplyVersionSummary(message, version);
            return MarisaPluginTaskState.CompletedTask;
        }

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
            var player = ResolvePlayer(message);
            var (_, scores) = await player.Fetcher.GetScores(player);

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

        var player = ResolvePlayer(message);
        var (_, scores) = await player.Fetcher.GetScores(player);

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
        "用法：mai <范围><目标成绩><谱面难度>完成表\n" +
        "范围、目标成绩、谱面难度的顺序不固定。\n" +
        "\n" +
        "范围必须填写；多个范围条件需同时满足，多个谱面难度满足任一即可。\n" +
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
        "谱面难度不写时，普通版本代字默认只查紫谱；舞和其他范围默认查紫谱和白谱。\n" +
        "指定等级或定数时，或只查宴会场时，默认查全难度。可显式指定一个或多个谱面难度：\n" +
        "  · 单项：绿谱 / 黄谱 / 红谱 / 紫谱 / 白谱，或 BSC / ADV / EXP / MST / Re:MASTER\n" +
        "  · 多项：用 / 连接，如 红谱/紫谱、EXP/MST；也支持“或”“、”，中文可直接连写，如 红谱紫谱\n" +
        "  · 区间：用 - 表示从低到高并包含两端，如 红谱-白谱、EXP-MST\n" +
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
        "  mai 真代13红谱/紫谱理论值完成表\n" +
        "  mai 镜代V家将完成表\n" +
        "  mai 14+四星完成表";

    public static MarisaPluginTrigger.PluginTrigger PlateTrigger => (message, _) =>
        message.Command.EndsWith(PlateData.CommandSuffix);

    [MarisaPluginDoc("按版本/谱师/类别/作曲家/等级/定数筛选完成表，支持多谱面难度")]
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

        var pairs = PlateData.SelectCharts(query!, SongDb.SongList);

        if (pairs.Count == 0)
        {
            message.Reply($"没有找到 {string.Join(" + ", query!.Selectors.Select(s => s.Display))} 对应的歌曲");
            return MarisaPluginTaskState.CompletedTask;
        }

        var player = ResolvePlayer(message);
        var (_, scores) = await player.Fetcher.GetScores(player);

        // 标题原样使用用户输入的命令文本（含"完成表"）。
        var im = await MaiMaiDraw.DrawPlateProgress(query!, pairs, scores, raw.Trim());
        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;

        static string FormatError(PlateData.ParseError err) => err.Kind switch
        {
            PlateData.ErrorKind.UnsupportedPlate => $"不支持该版本：{err.Detail}",
            PlateData.ErrorKind.InvalidDifficulty => $"谱面难度格式错误：{err.Detail}",
            PlateData.ErrorKind.UnknownSelector => $"无法识别版本/谱师/类别/作曲家/等级/定数：{err.Detail}",
            PlateData.ErrorKind.EmptyQuery => "'完成表' 前面要写一个版本代字 / 谱师名 / 类别 / 作曲家名 / 等级 / 定数",
            PlateData.ErrorKind.ConflictingSelector => $"{err.Detail}只能指定一次",
            _ => "命令格式错误",
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

        var player = ResolvePlayer(message);
        var rating = await player.Fetcher.GetRating(player);

        var result = CreateRecommendationEngine().BuildPlan(rating, target);
        switch (result.Status)
        {
            case MaiMaiRecommendationPlanStatus.AlreadyReached:
                message.Reply($"当前 Rating 已达到 {rating.Rating}，无需推分到 {target}");
                return MarisaPluginTaskState.CompletedTask;
            case MaiMaiRecommendationPlanStatus.Unreachable:
                message.Reply($"按照当前成绩无法规划到 Rating {target}");
                return MarisaPluginTaskState.CompletedTask;
        }

        var context = new WebContext();
        context.Put("recommendation", result.Data);
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
        var player = ResolvePlayer(message);
        var rating = await player.Fetcher.GetRating(player);
        var recommend = CreateRecommendationEngine().BuildQuick(rating);

        if (recommend.Items.Count == 0)
        {
            message.Reply("您无分可恰");
        }
        else
        {
            var context = new WebContext();
            context.Put("recommendation", recommend);
            message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiRecommend(context.Id)));
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
            var a = 96.9999;
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
                    var ret = "定数 -> 达成率 -> rating\n";

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
        var songName = message.Command.Trim();
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
            var dxScore = song.Charts[levelIdx].Notes.Sum() * 3;

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