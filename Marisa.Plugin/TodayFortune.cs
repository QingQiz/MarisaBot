namespace Marisa.Plugin;

[MarisaPluginDoc("魔理沙对你进行恶臭算命，得到你今天的音游运势")]
[MarisaPluginCommand(MessageType.GroupMessage, StringComparison.Ordinal, "今日运势", "jrys")]
public class TodayFortune : MarisaPluginBase
{
    private static int GenRandomSeed(long id)
    {
        var now = DateTime.Now;

        // 四个常数分别是：Prime[114514]、Prime[1919810]、Prime[114514 ^ 1919810]、Prime[114514 + 1919810]
        return (int)((((now.Year * now.Day * id) ^ 1504831) + ((now.Month * now.Day * id) ^ 31066753) +
                      ((now.Day  * now.Day * id) ^ 30680207)) % 33046393);
    }

    [MarisaPluginCommand(true, "")]
    private static MarisaPluginTaskState Handler(Message message)
    {
        var sender = message.Sender!.Id;
        var rand   = new Random(GenRandomSeed(sender));

        var config = ConfigurationManager.Configuration.Fortune;

        var e1 = config.Events.RandomTake(rand);
        var e2 = config.Events.RandomTake(rand);

        while (e2.EventName == e1.EventName)
        {
            e2 = config.Events.RandomTake(rand);
        }

        var pe = e1.Positive.RandomTake(rand);
        var ne = e2.Negative.RandomTake(rand);
        var g  = config.RhythmGames.RandomTake(rand);
        var d  = config.Direction.RandomTake(rand);
        var p = config.Position.RandomTake(rand);

        var now = DateTime.Now;

        var header =
            $"📅 今天是 {now:yyyy 年 M 月 d 日}\n⛄ 农历{ChinaDate.GetYear(now)}{ChinaDate.GetMonth(now)}{ChinaDate.GetDay(now)}";

        if (!string.IsNullOrEmpty(ChinaDate.GetChinaHoliday(now)))
        {
            header += $"，{ChinaDate.GetChinaHoliday(now)}";
        }

        message.Reply(
            new MessageDataText(header + "\n\n魔理沙掐指一算，"),
            new MessageDataAt(sender),
            new MessageDataText($" 今天：\n• 宜{e1.EventName}：{pe}\n• 忌{e2.EventName}：{ne}\n\n魔理沙为 "),
            new MessageDataAt(sender),
            new MessageDataText($" 推荐：\n• 今日音游：{g}\n• 打手机或平板音游最佳朝向：{d}\n• 街机音游黄金位：{p}")
        );

        return MarisaPluginTaskState.CompletedTask;
    }
}