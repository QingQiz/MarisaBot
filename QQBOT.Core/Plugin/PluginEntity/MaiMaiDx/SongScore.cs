namespace QQBOT.Core.Plugin.PluginEntity.MaiMaiDx
{
    public class SongScore
    {
        
        public double Achievement;
        public double Constant;
        public long DxScore;
        public string Fc;
        public string Fs;
        public string Level;
        public long LevelIdx;
        public string LevelLabel;
        public long Rating;
        public string Rank;
        public long Id;
        public string Title;
        public string Type;

        public SongScore(dynamic data)
        {
            Achievement = data.achievements;
            Constant    = data.ds;
            DxScore     = data.dxScore;
            Fc          = data.fc;
            Fs          = data.fs;
            Level       = data.level;
            LevelIdx    = data.level_index;
            LevelLabel  = data.level_label;
            Rating      = data.ra;
            Rank        = data.rate;
            Id          = data.song_id;
            Title       = data.title;
            Type        = data.type;
        }
    }
}