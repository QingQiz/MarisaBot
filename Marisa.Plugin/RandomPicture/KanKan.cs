using System.Configuration;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;
using Marisa.Utils;

namespace Marisa.Plugin.RandomPicture;

[MarisaPluginCommand("看看", "kk")]
[MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.PlainTextTrigger))]
public class KanKan : MarisaPluginBase
{
    private static readonly string PicDbPath = ConfigurationManager.AppSettings["PicDbPath_KanKan"]!;

    private static readonly List<string> PicDbPathExclude = new()
    {
        "R18", "backup"
    };

    private static readonly List<string> AvailableFileExt = new()
    {
        "jpg", "png", "jpeg"
    };

    private static IEnumerable<string?> Names =>
        Directory.GetDirectories(PicDbPath, "*", SearchOption.AllDirectories)
            .Where(d => PicDbPathExclude.All(e => !d.Contains(e)))
            .Select(d => d.TrimEnd('\\').TrimEnd('/'));

    private static readonly string[][] Alias =
    {
        new[] { "古明地恋", "恋", "小石头", "古明地三鲜", "514", "koishi" },
        new[] { "雾雨魔理沙", "魔理沙", "云里雾里沙", "金发孩子", "金可怜", "雨雾沙", "魔梨沙", "沙沙", "黑白", "莎莎", "摸你傻" },
        new[] { "博丽灵梦", "灵梦", "红白", "红白巫女", "无节操", "赤色杀人魔", "腋巫女" },
        new[] { "爱丽丝·玛格特罗依德", "爱丽丝", "小爱", "威震天" },
        new[] { "帕秋莉·诺蕾姬", "帕秋莉", "姆q", "帕琪" },
        new[] { "比那名居天子", "天子", "M子" },
        new[] { "伊吹萃香", "萃香", "西瓜" },
        new[] { "十六夜咲夜", "咲夜", "PAD长" },
        new[] { "蕾米莉亚·斯卡蕾特", "蕾米莉亚", "蕾米", "大小姐", "抱头蹲防" },
        new[] { "芙兰朵露·斯卡蕾特", "芙兰朵露", "芙兰", "二小姐", "二妹" },
        new[] { "西行寺幽幽子", "幽幽子", "uuz" },
        new[] { "魂魄妖梦", "妖梦", "曲林静树" },
        new[] { "东风谷早苗", "早苗", "苗爷" },
        new[] { "多多良小伞", "小伞" },
        new[] { "古明地觉", "觉", "satori", "小五", "⑤" },
        new[] { "灵乌路空", "阿空", "⑥", "蠢鸟" },
        new[] { "火焰猫燐", "阿燐", "猫车" },
        new[] { "丰聪耳神子", "神子", "二婶子" },
        new[] { "圣白莲", "莲妈" },
        new[] { "秦心", "心" },
        new[] { "二岩猯藏", "大狸子" },
        new[] { "少名针妙丸", "针妙丸", "小碗" },
        new[] { "封兽鵺", "鵺" },
        new[] { "村纱水蜜", "船长" },
        new[] { "鬼人正邪", "正邪", "政协", "邪正人鬼" },
        new[] { "红美铃", "红师傅", "美铃", "中国" },
        new[] { "哆来咪·苏伊特", "哆来咪", "123" },
        new[] { "爱塔妮缇·拉尔瓦", "拉尔瓦", "大扑棱蛾子" },
        new[] { "八坂神奈子", "神奈子", "神妈" },
        new[] { "洩矢诹访子", "诹访子", "青蛙子" },
        new[] { "射命丸文", "文文", "香港记者" },
        new[] { "河城荷取", "河童" },
        new[] { "赫卡提亚·拉碧斯拉祖利", "赫卡提亚", "赫卡", "五球女神" },
        new[] { "茨木华扇", "华扇", "包子仙人" },
        new[] { "摩多罗隐岐奈", "摩多罗" },
        new[] { "八意永琳", "永琳", "师匠" },
        new[] { "蓬莱山辉夜", "neet姬", "辉夜", "辉夜姬" },
        new[] { "铃仙·优昙华院·因幡", "铃仙", "受兔", "铃仙·U·因幡", "铃仙·优昙华院·稻叶" },
        new[] { "永江衣玖", "衣玖", "19", "皇带鱼" },
        new[] { "宇佐见堇子", "堇子" },
        new[] { "寅丸星", "大师兄", "老虎" },
        new[] { "娜兹玲", "纳兹玲", "娜兹琳", "老鼠", "最强一面" },
        new[] { "克劳恩皮丝", "皮丝", "美国妖精", "过膝袜" },
        new[] { "露米娅", "⑩" },
        new[] { "琪露诺", "⑨" },
        new[] { "藤原妹红", "妹红" },
        new[] { "上白泽慧音", "慧音", "老师" },
        new[] { "稗田阿求", "阿求" },
        new[] { "本居小铃", "小铃", "防撞桶" },
        new[] { "八云蓝", "蓝", "蓝妈" },
        new[] { "八云紫", "紫", "紫妈", "紫妹" },
        new[] { "橙", "橙喵" },
        new[] { "冈崎梦美", "草莓", "教授" },
        new[] { "赤蛮奇", "⑦" },
        new[] { "坂田合欢乃", "坂田合欢", "欢姐" },
        new[] { "姬海棠果", "果果" },
        new[] { "犬走椛", "椛椛" },
        new[] { "物部布都", "布嘟嘟" },
        new[] { "稀神探女", "探女", "黄旭东" },
        new[] { "小野塚小町", "乳町", "小町" },
        new[] { "四季映姬·亚玛萨那度", "四季映姬", "四季大人" },
        new[] { "因幡帝", "因幡天为", "帝帝", "小老帝", "腹黑兔" },
        new[] { "水桥帕露西", "桥姬", "帕露西" },
        new[] { "依神紫苑", "藤原妹蓝" },
        new[] { "莉莉白", "莉莉霍瓦特" },
        new[] { "霍青娥", "青娥娘娘", "娘娘" },
        new[] { "米斯蒂娅·萝蕾拉", "米斯蒂娅", "小碎骨", "老板娘" },
        new[] { "幽谷响子", "复读机" },
        new[] { "梅蒂欣·梅兰可莉", "梅蒂欣", "毒人偶" },
        new[] { "风见幽香", "幽香", "花妈" },
        new[] { "大妖精", "大酱" },
        new[] { "陈睿", "cr", "叔叔" },
        new[] { "初音未来", "miku" }
    };

    private static List<string> GetImList(string name)
    {
        return Directory
            .GetFiles(name, "*.*", SearchOption.AllDirectories)
            .Where(fn => PicDbPathExclude.All(ex => !fn.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            .Where(fn =>
                AvailableFileExt.Any(ext => fn.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    [MarisaPluginCommand]
    private static MarisaPluginTaskState Handler(Message m)
    {
        var n = m.Command;

        if (string.IsNullOrWhiteSpace(n))
        {
            if (new Random().Next(10) < 3)
            {
                m.Reply("看你妈，没有，爬！");
            }
            else
            {
                m.Reply(MessageDataImage.FromPath(Path.Join(ConfigurationManager.AppSettings["Help"]!, "kk.jpg")), false);
            }

            return MarisaPluginTaskState.CompletedTask;
        }

        n = Alias.FirstOrDefault(@as => @as.Any(a => a.Equals(n, StringComparison.OrdinalIgnoreCase)))?.First() ?? n;

        var d = Names.FirstOrDefault(d => Path.GetFileName(d)!.Equals(n, StringComparison.OrdinalIgnoreCase));

        if (d == null)
        {
            return MarisaPluginTaskState.NoResponse;
        }

        var pic = GetImList(d).RandomTake();

        m.Reply(pic.Replace(PicDbPath, ""));
        m.Reply(MessageDataImage.FromPath(pic), false);

        return MarisaPluginTaskState.CompletedTask;
    }
}