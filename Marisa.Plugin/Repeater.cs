namespace Marisa.Plugin;

[MarisaPlugin(PluginPriority.Repeater)]
[MarisaPluginCommand(MessageType.GroupMessage)]
public class Repeater: MarisaPluginBase
{
    private static readonly Dictionary<long, (string m, int t)> RepeaterStatus = new();
    private static readonly (int fst, int snd) ThreshHold = (3, 9);

    [MarisaPluginCommand]
    private static MarisaPluginTaskState Handler(Message m)
    {
        lock (RepeaterStatus)
        {
            if (!m.IsPlainText())
            {
                RepeaterStatus.Remove(m.Location);
                return MarisaPluginTaskState.NoResponse;
            }

            if (!RepeaterStatus.ContainsKey(m.Location))
            {
                RepeaterStatus[m.Location] = (m.Command, 1);
                return MarisaPluginTaskState.NoResponse;
            }

            var s = RepeaterStatus[m.Location];

            if (m.Command == s.m)
            {
                s.t += 1;
            }
            else
            {
                s = (m.Command, 1);
            }

            RepeaterStatus[m.Location] = s;

            if (s.t == ThreshHold.fst)
            {
                m.Reply(m.Command, false);
            }

            if (s.t == ThreshHold.snd)
            {
                m.Reply("复读你🐴呢", false);
            }

            return MarisaPluginTaskState.NoResponse;
        }
    }
}