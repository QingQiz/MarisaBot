using Marisa.Database;
using Marisa.Database.Entity.Plugin;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin;

[MarisaPluginDoc("解决**中午吃什么**这一人生 N 大难题之一")]
[MarisaPluginTrigger(nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
public class Chi : MarisaPluginBase
{
    private const int Times = 5;
        private static readonly string[] DefaultMeals =
    [
        // ================== 【正常/经典家常炒菜（下饭神菜）】 ==================
        "糖醋里脊（酸甜口YYDS）",
        "麻婆豆腐",
        "地三鲜（东北菜的灵魂）",
        "回锅肉",
        "辣子鸡丁",
        "蒜苔炒肉",
        "干锅土豆片",
        "铁板日本豆腐",
        "鱼香茄子",
        "西红柿鸡蛋（永远的经典）",
        "葱爆羊肉",
        "木须肉",
        "青椒炒肉丝",
        "干煸四季豆",
        "溜肉段",
        "爆炒肥肠（爱的人爱死）",
        "金汤肥牛",

        // ================== 【正常/下馆子与聚餐类】 ==================
        "东门外苍蝇馆子",
        "外婆印象",
        "绿茶餐厅",
        "东北铁锅炖（记得多加贴饼子）",
        "新疆炒米粉（微辣也是辣！）",
        "随便找家日料吃吃",
        "酸菜鱼（多吃鱼变聪明）",
        "农家小炒肉",
        "水煮肉片",
        "烤鱼",
        "潮汕牛肉火锅",
        "毛血旺",
        "串串香",
        "冒菜（一个人的火锅）",
        "北京烤鸭",
        "椰子鸡火锅（清淡养生）",
        "牛蛙火锅/干锅牛蛙",
        "猪肚鸡",

        // ================== 【正常/食堂与外卖快餐盖饭】 ==================
        "盖浇饭",
        "牛肉饭",
        "麻辣拌",
        "水果捞",
        "炒面or炒饭",
        "喝粥吃饼",
        "清真食堂烤又饭",
        "麻辣香锅",
        "食堂自选菜（称重刺客）",
        "铁板烧",
        "食堂阿姨手抖特供打饭（看阿姨心情）",
        "鱼香肉丝盖饭",
        "宫保鸡丁盖饭",
        "咖喱鸡肉饭",
        "照烧鸡排饭",
        "台湾卤肉饭",
        "日式蛋包饭",
        "脆皮五花肉饭",
        "蜜汁叉烧饭",
        "排骨米饭",
        "黄焖排骨/黄焖猪脚",
        "滑蛋虾仁饭",
        "木桶饭",
        "石锅拌饭",
        "烤肉拌饭",
        "烧鸭饭",
        "白切鸡饭",

        // ================== 【正常/面、粉、米线】 ==================
        "二楼食堂的面",
        "重庆小面",
        "老碗面",
        "清真兰州拉面",
        "牛阿达喊你来吃面了，先喝汤，后面爷忘了",
        "鸡汤刀削面",
        "油泼刀削面",
        "炸酱刀削面",
        "炒刀削面",
        "阿香李现",
        "宽窄巷",
        "哥妹俩土豆粉",
        "魏儿家儿凉儿皮儿",
        "黄焖鸡米饭",
        "池奈（吃奶？",
        "沙县大酒店（点个鸭腿犒劳自己）",
        "螺蛳粉（在宿舍吃可能会被打，请注意安全）",
        "隆江猪脚饭（男人的终极浪漫）",
        "广式煲仔饭",
        "葱油拌面",
        "四川担担面",
        "武汉热干面",
        "鸭血粉丝汤",
        "砂锅米线",
        "新疆拌面/拉条子",
        "干炒牛河",
        "云南过桥米线",
        "桂林米粉",
        "锡纸花甲粉",
        "肥肠面",
        "猪肝面",
        "西红柿鸡蛋面",
        "朝鲜冷面（夏天吃绝了）",

        // ================== 【正常/包子点心与碳水小吃】 ==================
        "红油抄手/馄饨",
        "小笼包（小心烫嘴）",
        "生煎包",
        "锅贴/煎饺",
        "水饺（韭菜鸡蛋还是猪肉大葱？）",
        "广东肠粉",
        "虾饺与广式早茶",
        "肉夹馍套餐（三秦套餐走起）",
        "杂粮煎饼（多加脆薄脆）",
        "烧麦（纯碳水包碳水，快乐翻倍）",

        // ================== 【正常/西餐、洋快餐与异国料理】 ==================
        "开封菜（狂乱木曜日限定）",
        "麦当劳（今天我是麦门信徒）",
        "必胜客",
        "那家汉堡很厚的汉堡店",
        "喷射战士（不想吃可以再抽一次）",
        "八嘎King",
        "萨莉亚（意大利沙县大酒店，穷鬼乐园）",
        "赛百味（假装今天吃得很健康）",
        "达美乐（外卖员竞速测试）",
        "经典意大利肉酱面",
        "墨西哥塔可饼（Taco）",
        "猪排盖饭（Katsudon）",
        "鳗鱼饭",
        "寿司拼盘",
        "咖喱乌冬面",
        "冬阴功汤",
        "泰式菠萝炒饭",
        "越南河粉（Pho）",
        "披萨（芝士就是力量）",
        "原切牛排",

        // ================== 【正常/小摊与走街串巷】 ==================
        "西门小摊的炒面",
        "孜然夹馍",
        "烤！面！筋！",
        "纯纯纯纯淀粉烤肠（吃不饱，记得再来一次吃啥）",
        "炸鸡柳（吃不饱，记得再来一次吃啥）",
        "炸串夹/拌馍",
        "烤冷面（加卫龙加金针菇）",
        "鸡蛋灌饼",
        "正宗长沙臭豆腐（大概率不正宗）",
        "关东煮（汤底年份成谜）",
        "章鱼小丸子（基本全是面糊子）",
        "掉渣饼",
        "煎饼果子（加两个蛋）",
        "正宗陕西肉夹馍",
        "淮南牛肉汤",
        "烤红薯（冬天必备）",
        "糖炒栗子",
        "铁板鱿鱼",
        "钵钵鸡",
        "盐酥鸡/鸡排",

        // ================== 【正常/轻食、杂食与凑合】 ==================
        "海底捞火锅（工作日限定）",
        "呷哺呷哺",
        "沪国圣剑",
        "大白印象城烤肉",
        "汉城烧烤",
        "三汁儿闷锅",
        "老板 娘的烤肉店",
        "要不试试踩个雷？踩完记得回来加到列表里",
        "买个泡面得了",
        "韩国拌饭",
        "饺满天下！",
        "去便利店（罗森/711/全家）买个便当凑合",
        "吃草（轻食沙拉，今天减肥！）",
        "去蹭朋友/室友的饭（需要极度厚脸皮）",
        "日式寿喜锅",
        "韩式部队锅",
        "水煮菜配粗粮（极致自律）",
        "全麦面包配无糖豆浆",
        "凉拌菜配白粥",
        "燕麦片泡牛奶",

        // ================== 【发疯/绝望/概念级食物（低爆率）】 ==================
        "西北风（富含负氧离子，零卡零糖）",
        "辟谷（今天修仙，不吃了）",
        "气都气饱了，吃什么吃！",
        "吃土（月底/双十一后限定版）",
        "老板/导师画的饼（又大又圆，就是有点噎人）",
        "咽口水骗一下胃（胃：你当我是傻子吗？）",
        "吃点亏（反正平时也没少吃）",
        "吃爱情的苦（哦，单身狗吃不到，那没事了）",
        "看吃播（假装自己吃过了，赛博意念进食法）",
        "去超市试吃区吃一顿免费自助（需要极高的脸皮厚度）",
        "键盘缝里的陈年饼干屑（属于考古大发现）",
        "室友昨天吃剩的半个包子（或许已经能拉丝了）",
        "摸摸肚子上的肉，你还吃得下？（来自系统的灵魂拷问）",
        "Error 404: 饭 Not Found，建议重启肉体",
        "吃空气（高逼格极简主义分子料理）",
        "今天算卦说忌进食，饿着吧！",
        "薛定谔的饭：只要我不饿，我就吃过了",

        // ================== 【抽象/重度生化武器与不可名状之物（极低爆率）】 ==================
        "狗屎",
        "油炸臭鞋垫",
        "九转大肠（保留了一点原味的那种）",
        "老八秘制小汉堡（奥利给干了兄弟们！）",
        "猫砂盆盲盒（纯手工现刨，保证热乎）",
        "新鲜的奥利给",
        "红烧二手键盘（全是陈年包浆）",
        "清蒸没洗过的拖把头",
        "凉拌钢丝球（富含铁元素，嚼劲十足）",
        "炭烤米其林废旧轮胎",
        "蒜蓉粉丝蒸老鼠药",
        "洁厕灵兑雪碧（深海冰蓝微醺气泡水）",
        "铀235刺身（吃完整个人都发光了）",
        "鲱鱼罐头拌榴莲（吃完可以合法申请生化武器袭击）",
        "下水道现捞地沟油刺身",
        "板蓝根泡面（大郎，该吃面了）",
        "老陈醋泡原味袜子",
        "生啃两口墙皮",
        "苍蝇馆子里的纯正苍蝇刺身",
        "别人吐出来的口香糖（还能吹泡泡呢）",
        "嚼两口玻璃碴子（嘎嘣脆，满嘴血）",
        "脑残片（建议加大剂量）"
    ];

    private readonly Dictionary<long, (DateTime, int)> _cache = new();
    private readonly object _dataLock = new();
    private bool _defaultPlaceInitialized;

    private bool Zuo(long id)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(id, out var value))
            {
                var (time, cnt) = value;
                if (DateTime.Now - time < TimeSpan.FromMinutes(5))
                {
                    if (cnt >= Times) return true;

                    _cache[id] = (DateTime.Now, cnt + 1);
                    return false;
                }

                _cache[id] = (DateTime.Now, 1);
                return false;
            }

            _cache[id] = (DateTime.Now, 1);
            return false;
        }
    }

    private readonly Dictionary<string, HashSet<string>> _data = new();

    public Chi()
    {
        using var realm = BotDbContext.OpenRealm();
        foreach (var i in realm.All<Meal>())
        {
            AddMeal(i.Place, i.Name);
        }
    }

    private void EnsureDefaultPlaceConfigured()
    {
        lock (_dataLock)
        {
            if (_defaultPlaceInitialized) return;

            if (_data.TryGetValue("西工大", out var meals))
            {
                meals.UnionWith(DefaultMeals);
            }
            else
            {
                _data["西工大"] = DefaultMeals.ToHashSet();
            }

            _defaultPlaceInitialized = true;
        }
    }

    private void AddMeal(string place, string meal)
    {
        if (_data.TryGetValue(place, out var value))
        {
            value.Add(meal);
        }
        else
        {
            _data[place] = [meal];
        }
    }

    private string ChiSha(long id, string place)
    {
        EnsureDefaultPlaceConfigured();

        if (Zuo(id))
        {
            return "生吃你妈 问这么多还不知道吃啥饿死你个臭傻逼";
        }

        lock (_data)
        {
            if (!string.IsNullOrWhiteSpace(place) && _data.TryGetValue(place, out var value) && value.Count > 0)
            {
                return value.RandomTake(1).First();
            }
            return _data.Values.SelectMany(x => x).RandomTake(1).FirstOrDefault() ?? "还没有可选的，先给我加点吃的吧";
        }
    }

    [MarisaPluginTrigger(typeof(Chi), nameof(Trigger))]
    private MarisaPluginTaskState Proc(Message message)
    {
        var sender = message.Sender.Id;
        var cmd    = message.Command;

        var place = cmd.EndsWith("啥") ? cmd[..^2] : cmd[..^3];

        message.Reply(ChiSha(sender, place.ToString()));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("现有可用的地点")]
    [MarisaPluginCommand(true, "listplace")]
    private MarisaPluginTaskState Place(Message message)
    {
        EnsureDefaultPlaceConfigured();

        lock (_data)
        {
            var places = _data
                .Where(x => x.Value.Count != 0)
                .Select(x => x.Key)
                .ToList();
            var reply = "现有可用的地点：\n" + string.Join("\n", places);
            message.Reply(reply);
        }

        return MarisaPluginTaskState.CompletedTask;
    }


    [MarisaPluginDoc("添加吃啥的可选项", "`地点`")]
    [MarisaPluginCommand("addmeal")]
    private MarisaPluginTaskState Add(Message message)
    {
        EnsureDefaultPlaceConfigured();

        var place = message.Command.ToString();

        if (place.Any(char.IsPunctuation))
        {
            message.Reply("sb");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("吃什么？");

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var meal = next.Command.ToString();
            lock (_data)
            {
                AddMeal(place, meal);
            }

            Task.Run(() =>
            {
                using var realm = BotDbContext.OpenRealm();
                realm.Write(() => realm.AddWithAutoId(new Meal(place, meal)));
            });
            message.Reply($"{place} 已添加菜品 {meal}");

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }, this);

        return MarisaPluginTaskState.CompletedTask;
    }


    [MarisaPluginDoc("删除吃啥的可选项", "`地点`")]
    [MarisaPluginCommand("delmeal")]
    private MarisaPluginTaskState DeleteMeal(Message message)
    {
        EnsureDefaultPlaceConfigured();

        var commanders = ConfigurationManager.Configuration.Commander;

        if (!commanders.Contains(message.Sender.Id))
        {
            message.Reply("你没资格啊，你没资格。正因如此，你没资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        var place = message.Command.ToString();

        lock (_data)
        {
            if (!_data.ContainsKey(place))
            {
                message.Reply("无");
                return MarisaPluginTaskState.CompletedTask;
            }
        }

        lock (_data)
        {
            message.Reply($"删什么？ \n{string.Join('\n', _data[place])}");
        }

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var meal = next.Command.ToString();
            lock (_data)
            {
                _data[place].Remove(meal);
            }

            Task.Run(() =>
            {
                using var realm = BotDbContext.OpenRealm();
                realm.Write(() => realm.RemoveRange(realm.All<Meal>().Where(x => x.Place == place && x.Name == meal)));
            });
            message.Reply($"{place} 已删除菜品 {meal}");

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }, this);

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("删除吃啥的地点", "`地点`")]
    [MarisaPluginCommand("delplace")]
    private MarisaPluginTaskState DeletePlace(Message message)
    {
        EnsureDefaultPlaceConfigured();

        var commanders = ConfigurationManager.Configuration.Commander;

        if (!commanders.Contains(message.Sender.Id))
        {
            message.Reply("你没资格啊，你没资格。正因如此，你没资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        var place = message.Command.ToString();

        lock (_data)
        {
            if (!_data.ContainsKey(place))
            {
                message.Reply("无");
                return MarisaPluginTaskState.CompletedTask;
            }
            _data[place] = [];
        }

        Task.Run(() =>
        {
            using var realm = BotDbContext.OpenRealm();
            realm.Write(() => realm.RemoveRange(realm.All<Meal>().Where(x => x.Place == place)));
        });
        message.Reply("删完了");
        return MarisaPluginTaskState.CompletedTask;
    }

    public static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        if (!message.IsPlainText()) return false;

        return message.Command.EndsWith("吃什么", StringComparison.OrdinalIgnoreCase) ||
               message.Command.EndsWith("吃啥", StringComparison.OrdinalIgnoreCase);
    };
}
