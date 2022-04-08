namespace QQBot.Plugin.Shared.MaiMaiDx
{
    public class MaiMaiSongChart
    {
        public readonly List<long> Notes = new();

        public MaiMaiSongChart(dynamic data)
        {
            foreach (var n in data.notes) Notes.Add(n);
        }
    }
}