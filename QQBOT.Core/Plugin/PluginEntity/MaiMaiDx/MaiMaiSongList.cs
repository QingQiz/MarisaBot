using System.Collections.Generic;

namespace QQBOT.Core.Plugin.PluginEntity.MaiMaiDx
{
    public class MaiMaiSong
    {
        public string Id;
        public string Title;
        public string Type;
        public List<double> Constants = new();
        public List<string> Levels = new();
        public List<MaiMaiSongChart> Charts = new();
        public MaiMaiSongInfo Info;

        public MaiMaiSong(dynamic data)
        {
            Id        = data.id;
            Title     = data.title;
            Title     = Title.Trim();
            Type      = data.type;
            Info      = new MaiMaiSongInfo(data.basic_info);

            // 好像只能这样写。。。好丑。。。
            foreach (var c in data.ds)
            {
                Constants.Add(c);
            }

            foreach (var l in data.level)
            {
                Levels.Add(l);
            }

            foreach (var c in data.charts)
            {
                Charts.Add(new MaiMaiSongChart(c));
            }
        }
    }

    public class MaiMaiSongChart
    {
        public List<long> Notes = new();
        public string Charter;

        public MaiMaiSongChart(dynamic data)
        {
            Charter = data.charter;
            foreach (var n in data.notes)
            {
                Notes.Add(n);
            }
        }
    }

    public class MaiMaiSongInfo
    {
        public string Title;
        public string Artist;
        public string Genre;
        public long Bpm;
        public string ReleaseData;
        public string From;
        public bool IsNew;

        public MaiMaiSongInfo(dynamic data)
        {
            Title       = data.title;
            Title       = Title.Trim();
            Artist      = data.artist;
            Genre       = data.genre;
            Bpm         = data.bpm;
            ReleaseData = data.release_date;
            From        = data.from;
            IsNew       = data.is_new;
        }
    }
}