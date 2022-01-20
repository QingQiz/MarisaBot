using System.Drawing;

namespace QQBot.Plugin.Shared.Util.SongDb;

public abstract class Song
{
    public long Id;
    public string Title = "";
    public string Artist = "";


    public abstract string MaxLevel();
    public abstract string GetImage();

    public abstract Bitmap GetCover();
}