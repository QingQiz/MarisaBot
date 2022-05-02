namespace Marisa.Plugin.Shared.MaiMaiDx
{
    public class MaiMaiSongInfo
    {
        public string Title;
        public string Artist;
        public string Genre;
        public long Bpm;
        public string ReleaseDate;
        public string From;
        public bool IsNew;

        public MaiMaiSongInfo(dynamic data)
        {
            Title       = data.title;
            Title       = Title.Trim();
            Artist      = data.artist;
            Genre       = data.genre;
            Bpm         = data.bpm;
            ReleaseDate = data.release_date;
            From        = data.from;
            IsNew       = data.is_new;
        }
    }
}