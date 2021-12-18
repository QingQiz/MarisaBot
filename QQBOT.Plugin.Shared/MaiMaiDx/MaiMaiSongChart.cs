namespace QQBot.Plugin.Shared.MaiMaiDx
{
    public class MaiMaiSongChart
    {
        public List<long> Notes = new();
        public string Charter;

        public MaiMaiSongChart(dynamic data)
        {
            Charter = data.charter;
            foreach (var n in data.notes) Notes.Add(n);
        }
    }
}