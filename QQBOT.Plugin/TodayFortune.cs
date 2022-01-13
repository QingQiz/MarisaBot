using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin;

[MiraiPluginCommand("今日运势", "jrys")]
public class TodayFortune : MiraiPluginBase
{
    // 什么硬编码 (
    // 宜
    private static readonly string[] PEvent =
    {
        "收歌：又有资本可以炫耀了。",
        "收歌：排行榜第一就在下一次！",
        "收歌：攻克卡住多时的难点。",
        "收歌：手感爆棚，曲曲理论值。",
        "炫耀成绩：获得万众敬仰，收割万人膝盖。",
        "炫耀成绩：一曲成名，万人膜拜。",
        "炫耀成绩：本群都是你的小弟啦！",
        "复读：有时候，人云亦云也是一种生存方式。",
        "肝爆：说不定下一盘就创纪录了呢。",
        "肝爆：大力出奇迹。",
        "肝爆：努力使人进步，肝爆让人快乐。",
        "肝爆：限时梯子爬完了吗？",
        "课金：早买早享受，晚买哭着求。",
        "课金：看到这个新曲子了吗？我有了！",
        "出勤：有机会与大佬切磋交流，获得音游经验值双倍加成。",
        "出勤：今日的机厅无人打扰，获得霸机机会。",
        "出勤：会忽然成为机厅中的焦点人物。",
        "卖弱：楚楚动人更容易打动群友。",
        "唱脑力：唱一次提升醒脑，唱两次精神百倍。",
        "刷剧情：会度过充实的一天。",
        "爬梯子：今天就可以到顶哦。",
        "看手元：从手元中获得一点音游经验。",
        "录手元：音游届的未来新星 up 主就是你。",
        "开浴霸：不如跳舞，玩音游不如跳舞。",
        "研究魔王曲谱面：发现收歌的秘诀。",
        "挑战魔王曲：一上来就是一个新纪录。",
        "咕咕咕：一时咕一时爽。",
        "与群友水聊：扶我起来我还能打字。",
        "考段：底力飞升，什么都能糊过去，考段稳过。",
        "吃萨利亚：毕竟是音游狗餐厅。",
        "打课题：突然就获得最高积分。",
        "面基：两个大佬一起，给魔王曲带来意想不到的灾难。",
        "制谱：文思泉涌、脑洞大开，一不小心制作出十分优良的谱面。",
        "吃葡萄：音游狗水果，好吃的水果。",
        "催更：明天音游厂家就更新。",
        "迫害大佬：迫害是大佬进步的阶梯。",
        "迫害大佬：迫害语录 +1。",
        "唱打：大家快看，这个唱歌好听的小哥哥（小姐姐）手速怎么这么快？",
        "唱打：唱歌又好听，手速又超快，超喜欢在里面 der。",
        "算命：不遵守你的 IM 帐户待会就被封。",
        "算命：算啥都准。",
        "挑战单手过关：一只更比两只强。",
    };

    // 忌
    private static readonly string[] NEvent =
    {
        "收歌：快要收歌的时候会来一通无关紧要的电话。",
        "收歌：会在最后关头卡帧掉 note。",
        "收歌：延迟漂移个个 Good，曲曲延迟各不同。",
        "收歌：看见这个 note 了吗？你 接 不 住。",
        "炫耀成绩：警察叔叔，对，就是这个杀人犯。",
        "炫耀成绩：容易被群友拉黑或报复。",
        "复读：你的对手是鸽子。",
        "肝爆：会因为腱鞘炎而进医院。",
        "肝爆：屏幕会碎掉的。",
        "肝爆：醒醒，限时活动没了。",
        "课金：第二天就 50% off。",
        "课金：银行卡余额不足，支付失败。",
        "课金：接下来的一个月只能吃土。",
        "出勤：会不小心把机器拍坏。",
        "出勤：今天商场停电的概率比较高。",
        "出勤：音游街机前全是众众众众众众众众。",
        "出勤：会被大佬碾压。",
        "卖弱：Boy♂︎next♂︎door。",
        "唱脑力：会与复读机一起，对群聊造成不可逆的毁灭打击。",
        "刷剧情：会被虐到。",
        "爬梯子：会不小心把所有碎片全用来爬梯子。",
        "看手元：会被大佬闪瞎。",
        "录手元：打完歌才发现忘记开录像。",
        "开浴霸：会把家/宿舍点着的。",
        "研究魔王曲谱面：你个凡人还想收魔王？Naive！",
        "挑战魔王曲：有这点时间还不如干点别的。",
        "咕咕咕：会被抓起来，被群友唾沫淹死。",
        "与群友水聊：一不小心就被大佬闪瞎。",
        "考段：眼花缭乱，各种看不清，怎么考都会在最后crash。",
        "吃萨利亚：真的不选对面那个吉野家吗？",
        "打课题：别人成绩都比你高，于是自闭。",
        "面基：会连烤全鸽都吃不到。",
        "制谱：绞尽脑汁却想不出做出什么谱面，只能加入钟国谱面大军。",
        "吃葡萄：白内障看不清，杀铺 Ice 滴眼睛。",
        "催更：就知道催，老谱没收催什么催？",
        "迫害大佬：亲爱的，你号没了。",
        "迫害大佬：管理清一下？",
        "迫害大佬：喝茶警告。",
        "唱打：唱，跳，rap，再来个篮球，你就是音游区最蔡的仔。",
        "算命：老黄历因为算太多刷屏被群管理封号。",
        "算命：诸事不宜。",
        "挑战单手过关：不需要的手可以捐给有需要的人。",
    };

    // 游戏列表
    private static readonly string[] GList =
    {
        "O.N.G.E.K.I.", "太鼓达人", "吉他英雄", "デレステ", "音灵", "D4DJ Groovy Mix", "Project DIVAFuture Tone", "节奏海拉鲁",
        "命运歌姬", "Dance Dance Revolution", "不可思议乐队", "Guitar Hero", "Project Sekai (プロセカ)", "节奏大师", "GROOVE COASTER",
        "Love Live", "osu!", "Deemo", "Malody", "Dynamix", "同步音律喵赛克", "OverRapid", "VOEZ", "Lanota", "Arcaea", "阳春白雪",
        "Cytus II", "Mush Dash", "Phigros", "Beatmania IIDX", "Orzmic", "WACCA", "Beat Saber", "BanG Dream! 少女乐团派对!",
        "东方弹幕神乐", "DJMAX", "冰与火之舞", "maimaiDX", "CHUNITHM", "音击", "jubeat", "SOUND VOLTEX"
    };

    // 朝向
    private static readonly string[] DList = { "东", "南", "西", "北", "东南", "西南", "东北", "西北" };

    // 机位
    private static readonly string[] PList = { "P1", "P2", "P1", "P2", "P1", "P2", "P1", "P2", "维修位" };

    private static readonly Dictionary<long, DateTime> Cache = new();
    private static readonly DateTime BeginTime = new(2021, 1, 4, 5, 14, 0, 0);

    private static int GenRandomSeed(long id)
    {
        var now = DateTime.Now;

        if (Cache.ContainsKey(id))
        {
            var t = Cache[id];

            if (t.Day == now.Day && t.Month == now.Month)
            {
                return (int)((t - BeginTime).TotalSeconds * 10);
            }
        }

        Cache[id] = now;
        return (int)((now - BeginTime).TotalSeconds * 10);
    }

    [MiraiPluginCommand(true, "")]
    private static MiraiPluginTaskState Handler(Message message, MessageSenderProvider ms)
    {
        var sender = message.Sender!.Id;
        var rand   = new Random(GenRandomSeed(sender));

        var pe = PEvent[rand.Next(PEvent.Length)];
        var ne = NEvent[rand.Next(NEvent.Length)];
        var g  = GList[rand.Next(GList.Length)];
        var d  = DList[rand.Next(DList.Length)];
        var p  = PList[rand.Next(PList.Length)];

        var now = DateTime.Now;

        var header =
            $"📅 今天是 {now:yyyy 年 M 月 d 日}\n⛄ 农历 {ChinaDate.GetYear(now)}{ChinaDate.GetMonth(now)}{ChinaDate.GetDay(now)}";

        if (!string.IsNullOrEmpty(ChinaDate.GetChinaHoliday(now)))
        {
            header += $"，{ChinaDate.GetChinaHoliday(now)}";
        }

        ms.Reply(new MessageChain(new MessageData[]
        {
            new PlainMessage(header + "\n\n魔理沙掐指一算，"),
            new AtMessage(sender),
            new PlainMessage($" 今天：\n• 宜{pe}\n• 忌{ne}\n\n魔理沙为 "),
            new AtMessage(sender),
            new PlainMessage($" 推荐：\n• 今日音游：{g}\n• 打手机或平板音游最佳朝向：{d}\n• 街机音游黄金位：{p}"),
        }), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    [MiraiPluginCommand(true, "reset")]
    private static MiraiPluginTaskState Reset(Message message, MessageSenderProvider ms)
    {
        const long authorId = 642191352L;
        var        sender   = message.Sender!.Id;

        if (sender == authorId)
        {
            Cache.Clear();
            ms.Reply("Success", message);
            return MiraiPluginTaskState.CompletedTask;
        }
        else
        {
            ms.Reply("Denied", message);
            return MiraiPluginTaskState.CompletedTask;
        }
    }
}