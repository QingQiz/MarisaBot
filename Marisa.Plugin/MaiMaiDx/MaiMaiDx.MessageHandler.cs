using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util;
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

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            if (!int.TryParse(next.Command.Span, out var idx) || idx < 0 || idx >= servers.Length)
            {
                next.Reply("错误的序号，会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            using var realm = BotDbContext.OpenRealm();

            var bind = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == next.Sender.Id);

            if (bind != null)
            {
                realm.Write(() => realm.Remove(bind));
            }

            realm.Write(() => realm.AddWithAutoId(new MaiMaiDxBind(next.Sender.Id, 0)
            {
                ServerName = servers[idx]
            }));

            message.Reply("好了");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }, this);

        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 推分同步（导）

    [MarisaPluginDoc("把成绩从华立(机台)推/导到查分器(水鱼/落雪)。首次需【私聊】配置一次令牌，之后群里也能直接用。基于 bakapiano/maimai-score-hub")]
    [MarisaPluginCommand("导", "导分")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private async Task<MarisaPluginTaskState> Sync(Message message)
    {
        var qq = message.Sender.Id;

        string? friendCode;
        using (var realm = BotDbContext.OpenRealm())
        {
            friendCode = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == qq)?.FriendCode;
        }

        // 已设置过 → 直接同步（群里也行，不需要任何机密）
        if (!string.IsNullOrWhiteSpace(friendCode))
        {
            await RunSync(message, friendCode!, null);
            return MarisaPluginTaskState.CompletedTask;
        }

        // 首次设置：要让用户发导入令牌，必须私聊
        if (message.GroupInfo != null)
        {
            message.Reply("首次使用「导」需要配置查分器令牌。请【私聊我】发送 `maimai 导` 完成一次设置（令牌不要发在群里，会泄露）。设置好以后群里也能直接用。");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply(
            "【首次设置 · 推分同步】\n" +
            "第一步：发送你的 maimai DX 好友码（游戏内「フレンド」里那串数字）。\n" +
            "之后我会用 maimai-score-hub 的机器人号给你发游戏内好友申请，你同意后我就能抓成绩并同步到查分器。"
        );

        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, message.Sender.Id), async fcMsg =>
        {
            var fc = fcMsg.Command.Trim().ToString().Trim();
            if (fc.Length is < 6 or > 20 || !fc.All(char.IsDigit))
            {
                fcMsg.Reply("好友码应该是一串数字，格式不对，已退出。可重新 `maimai 导`。");
                return MarisaPluginTaskState.Canceled;
            }

            fcMsg.Reply(
                "好。第二步：发送你要同步的查分器【导入令牌】（不是密码！）。\n" +
                "一行一个，至少一个，两个都发就两边都推：\n" +
                "  落雪 <你的落雪个人 API 令牌>\n" +
                "  水鱼 <你的水鱼 Import-Token>\n" +
                "令牌在哪拿：落雪→个人主页/开发者；水鱼→个人主页的「导入 token」。"
            );

            await DialogManager.AddDialogAsync((fcMsg.GroupInfo?.Id, fcMsg.Sender.Id), async tkMsg =>
            {
                var (lxns, df) = ParseTokens(tkMsg.Command.Trim().ToString());
                if (lxns == null && df == null)
                {
                    tkMsg.Reply("没识别到令牌（格式：`落雪 xxx` / `水鱼 xxx`），已退出。可重新 `maimai 导`。");
                    return MarisaPluginTaskState.Canceled;
                }

                // 只持久化好友码，令牌不落库
                using (var realm = BotDbContext.OpenRealm())
                {
                    var b = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == tkMsg.Sender.Id);
                    realm.Write(() =>
                    {
                        if (b == null)
                            realm.AddWithAutoId(new MaiMaiDxBind(tkMsg.Sender.Id, 0) { FriendCode = fc });
                        else
                            b.FriendCode = fc;
                    });
                }

                await RunSync(tkMsg, fc, (lxns, df));
                return MarisaPluginTaskState.CompletedTask;
            }, this);

            return MarisaPluginTaskState.CompletedTask;
        }, this);

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>跑一次完整同步：登录 → 轮询 → （可选设令牌）→ 推到所有已配置的查分器。</summary>
    private static async Task RunSync(Message message, string friendCode, (string? Lxns, string? DivingFish)? newTokens)
    {
        var msh = new MaiScoreHubClient();

        message.Reply("正在请求登录…（首次需要你在游戏里同意机器人的好友申请，已是好友则会直接抓分）");

        var login = await msh.LoginRequestAsync(friendCode);
        if (!string.IsNullOrEmpty(login.BotFriendCode))
        {
            message.Reply($"已用机器人账号（好友码 {login.BotFriendCode}）发起好友申请，请进游戏【同意好友】。我最多等 3 分钟，同意后会自动抓分并同步。");
        }

        // 轮询直到完成 / 失败 / 超时
        MaiScoreHubClient.LoginStatusResult? status = null;
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(5000);
            status = await msh.LoginStatusAsync(login.JobId);
            if (status.Done) break;
            if (status.Status is "failed" or "canceled")
            {
                message.Reply($"同步失败：{status.Message ?? status.Status}");
                return;
            }
        }

        if (status is not { Done: true } || string.IsNullOrEmpty(status.Token))
        {
            message.Reply("等待超时或没拿到登录凭据（多半是没及时同意好友申请）。同意后重试 `maimai 导` 即可。");
            return;
        }

        var jwt = status.Token!;

        // 设置新令牌（如有），再看 MSH 里配置了哪些查分器
        if (newTokens is { } t)
        {
            if (!string.IsNullOrWhiteSpace(t.Lxns)) await msh.SetTokenAsync(jwt, "lxns", t.Lxns!);
            if (!string.IsNullOrWhiteSpace(t.DivingFish)) await msh.SetTokenAsync(jwt, "diving-fish", t.DivingFish!);
        }

        var profile = await msh.GetProfileAsync(jwt);

        var targets = new List<string>();
        if (profile.HasLxns) targets.Add("lxns");
        if (profile.HasDivingFish) targets.Add("diving-fish");

        if (targets.Count == 0)
        {
            message.Reply("华立成绩已抓取，但还没配置任何查分器令牌。请【私聊我】重新 `maimai 导` 设置令牌。");
            return;
        }

        var sb = new StringBuilder("同步完成：\n");
        foreach (var p in targets)
        {
            var name = p == "lxns" ? "落雪" : "水鱼";
            try
            {
                var r = await msh.ExportAsync(jwt, p);
                sb.AppendLine(r.Success ? $"{name} ✅ 导入 {r.Exported}/{r.Scores} 条" : $"{name} ❌ {r.Message ?? "失败"}");
            }
            catch (Exception e)
            {
                sb.AppendLine($"{name} ❌ {e.Message}");
            }
        }

        message.Reply(sb.ToString().TrimEnd());
    }

    private static (string? Lxns, string? DivingFish) ParseTokens(string text)
    {
        string? lxns = null, df = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var sp  = line.IndexOfAny([' ', '\t', '：', ':']);
            var val = sp >= 0 ? line[(sp + 1)..].Trim() : "";

            if (line.StartsWith("落雪") || line.StartsWith("lxns", StringComparison.OrdinalIgnoreCase))
                lxns = val;
            else if (line.StartsWith("水鱼") || line.StartsWith("df", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("divingfish", StringComparison.OrdinalIgnoreCase) || line.StartsWith("diving-fish", StringComparison.OrdinalIgnoreCase))
                df = val;
        }

        return (string.IsNullOrWhiteSpace(lxns) ? null : lxns, string.IsNullOrWhiteSpace(df) ? null : df);
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

    [MarisaPluginDoc("让 AI 锐评你的 b50。末尾可加文风名指定风格（如「锐评 电棍」），不加则随机；「锐评 列表」看可选文风", "`[查分器账号名 / @某人 / 留空]` `[文风名]`")]
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
        var lv = message.Command.Trim();

        if (LvRegex().IsMatch(lv.ToString()))
        {
            var maxLv = lv.Span[^1] == '+' ? 14 : 15;
            var lvNr  = lv.Span[^1] == '+' ? lv[..^1] : lv;

            if (int.TryParse(lvNr.Span, out var i))
            {
                if (!(1 <= i && i <= maxLv))
                {
                    goto _error;
                }
            }
            else
            {
                goto _error;
            }
        }
        else
        {
            goto _error;
        }

            var fetcher = GetDataFetcher(message);
            var scores  = await fetcher.GetScores(message);

        var groupedSong = SongDb.SongList
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.song.Levels[data.i].Equals(lv, StringComparison.Ordinal))
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.constant.ToString("F1"));

        var im = await MaiMaiDraw.DrawGroupedSong(groupedSong, scores, lv.ToString());
            message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;

        // 集中处理错误
        _error:
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    private const string PlateUsage =
        "查询某个版本 / 谱师 / 类别 / 作曲家 / 难度 / 定数的完成情况，比如 mai 真大将完成表\n" +
        "\n" +
        "完整格式：mai (对象)(成绩)[难度]完成表\n" +
        "\n" +
        "(对象) — 必填，下面六类中至少给 1 个；也可以同时给多个，要求歌曲全部满足才入选：\n" +
        "  · 版本代字：真 / 超 / 橙 / 暁 / 熊 / 華 / 鏡 / 彩 …（后面加 '代' 也行，例如 熊代）\n" +
        "  · 谱师名：例如 翠楼屋（合作谱 'サファ太 vs 翠楼屋' 也算上）\n" +
        "  · 类别：术力口 / V家 / 东方 / 击中 / 流行 / 动漫 / 其他 / 宴会场 / 舞萌\n" +
        "  · 作曲家：例如 HIMEHINA、DECO*27（合作名义 'sasakure.UK x DECO*27' 也算上）\n" +
        "  · 难度 label：13 / 13+ / 14 / 14+ 等（游戏内显示难度）\n" +
        "  · 定数：13.5 / 14.7 等（必含小数点，1 位小数）\n" +
        "  注：同一类不能给两个（如 '镜代真'/'13+15'/'13+14.6' 会冲突报错）\n" +
        "\n" +
        "(成绩) — 不写就是 '将'（SSS）\n" +
        "  · 将=SSS / 大将=SSS+\n" +
        "  · 神=AP / 理论值=AP+ / 极=FC\n" +
        "  · 舞舞=FDX\n" +
        "  · 也可以直接写 SSS+ / SS / FC+ / AP+ / FDX+ 等\n" +
        "  · DX 分星档：一星~五星（或 1星~5星），对应 max DX 的 85/90/93/95/97%\n" +
        "\n" +
        "[难度] — 不写就是紫谱 + 白谱（MASTER + Re:MASTER）\n" +
        "  · 绿谱 / 黄谱 / 红谱 / 紫谱 / 白谱（白谱 = Re:MASTER）\n" +
        "  · 或英文缩写 BSC / ADV / EXP / MST\n" +
        "\n" +
        "其他例子（字段顺序可以随便换）：\n" +
        "  mai 真完成表             ← 阈值省略，默认 '将'\n" +
        "  mai 翠楼屋将完成表\n" +
        "  mai HIMEHINA神完成表\n" +
        "  mai 14+大将完成表        ← 难度 label\n" +
        "  mai 13.5神完成表         ← 定数\n" +
        "  mai 紫谱将真完成表       ← 字段顺序随便换\n" +
        "  mai 镜代13+AP完成表      ← 版本 + 难度 组合\n" +
        "  mai 镜代V家将完成表      ← 版本 + 类别 组合\n" +
        "  mai 14+翠楼屋将完成表    ← 难度 + 谱师 组合\n" +
        "  mai 镜代5星完成表        ← DX 分 5★ 完成情况\n" +
        "  mai 14+四星完成表        ← Lv14+ 全谱拿到 4★ 的情况";

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
            // 完成表默认 MASTER + Re:MASTER；用户显式给难度（红谱/EXPERT/...）则单元素 list 限定。
            var levelIdxes = q.LevelIdxes;
            return SongDb.SongList
                .SelectMany(song => song.Constants.Select((constant, i) => (constant, i, song)))
                .Where(t => levelIdxes.Contains(t.i))
                .Where(t => q.Selectors.All(sel => MatchSelector(sel, t.constant, t.i, t.song)))
                .Select(t => (t.constant, t.i, t.song))
                .ToList();
        }

        // 单 chart × 单 selector 的命中判断；handler 用 Selectors.All(...) 求 AND 交集。
        static bool MatchSelector(PlateData.Selector sel, double constant, int levelIdx, MaiMaiSong song) => sel switch
        {
            PlateData.Selector.Plate p =>
                p.Versions.Any(v => string.Equals(v, song.Version, StringComparison.OrdinalIgnoreCase)),

            // substring 匹配：兼容 "サファ太 vs 翠楼屋" 这种合作谱师名义。
            PlateData.Selector.Charter c =>
                levelIdx < song.Charters.Count
                && song.Charters[levelIdx].Contains(c.Name, StringComparison.OrdinalIgnoreCase),

            // song-level substring 匹配，兼容 "sasakure.UK x DECO*27" 这种合作作曲名义。
            PlateData.Selector.Artist a =>
                !string.IsNullOrEmpty(song.Info.Artist)
                && song.Info.Artist.Contains(a.Name, StringComparison.OrdinalIgnoreCase),

            // 谱师 ∪ 作曲家：处理 "rintaro soma" 这种身兼两职的人。
            PlateData.Selector.CharterOrArtist ca =>
                (levelIdx < song.Charters.Count
                 && song.Charters[levelIdx].Contains(ca.Name, StringComparison.OrdinalIgnoreCase))
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
                .Select(x => (SongDb.GetSongById(x.Id)!, x.LevelIdx, x.Achievement, x.Rating))
                .OrderByDescending(x => x.Item4),
            NewScores = rating.NewScores
                .Select(x => (SongDb.GetSongById(x.Id)!, x.LevelIdx, x.Achievement, x.Rating))
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
        if (double.TryParse(message.Command.Span, out var constant))
        {
            switch (constant)
            {
                case <= 15.0 and >= 1:
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
                case > 15:
                {
                    var result = new List<(double Constant, double Achievement)>();
                    var ret    = "定数 -> 达成率 -> rating\n";

                    Enumerable.Range(1, 150)
                        .Where(rat =>
                            SongScore.Ra(100.5, rat / 10.0) >= constant && SongScore.Ra(50, rat / 10.0) <= constant)
                        .ToList()
                        .ForEach(rat =>
                        {
                            var a = 49.0;
                            while (a < 100.5)
                            {
                                a = SongScore.NextRa(a, rat / 10.0);
                                var ra = SongScore.Ra(a, rat / 10.0);

                                if (ra != (int)constant) continue;

                                result.Add((rat / 10.0, a));
                                break;
                            }
                        });

                    ret += string.Join('\n',
                        result.Select(x => $"{x.Constant:00.0} -> {x.Achievement:000.0000} -> {(int)constant}"));

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

            var levelName   = MaiMaiSong.LevelNameAll.Concat(MaiMaiSong.LevelNameZh).ToList();
            var level       = levelName.FirstOrDefault(n => command.StartsWith(n, StringComparison.OrdinalIgnoreCase));
            var levelPrefix = level ?? "";
            if (level != null) goto RightLabel;

            level = levelName.FirstOrDefault(n =>
                command.StartsWith(n[0].ToString(), StringComparison.OrdinalIgnoreCase));
            if (level != null)
            {
                levelPrefix = command.Span[0].ToString();
                goto RightLabel;
            }

            next.Reply("错误的难度格式，会话已关闭。可用难度格式：难度全名、难度全名的首字母或难度颜色");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);

            RightLabel:
            var parseSuccess = double.TryParse(command[levelPrefix.Length..].Span, out var achievement);

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

            var levelIdx = levelName.IndexOf(level) % MaiMaiSong.LevelNameAll.Count;
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

    [GeneratedRegex(@"^[0-9]+\+?$")]
    private static partial Regex LvRegex();

    #endregion
}
