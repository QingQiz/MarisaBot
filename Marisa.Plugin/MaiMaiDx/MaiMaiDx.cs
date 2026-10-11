using Flurl.Http;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Util.SongGuessMaker;

namespace Marisa.Plugin.MaiMaiDx;

[MarisaPluginDoc("音游 maimai DX 的相关功能")]
[MarisaPlugin(PluginPriority.MaiMaiDx)]
[MarisaPluginCommand("maimai", "mai", "舞萌")]
public partial class MaiMaiDx :
    MarisaPluginBase,
    IHandleCommonException,
    ICanReset,
    IMarisaPluginWithHelp,
    IMarisaPluginWithRetrieve<MaiMaiSong>,
    IMarisaPluginWithCoverGuess<MaiMaiSong, MaiMaiDxGuess>

{
    private readonly MaiMaiSongCatalog _songCatalog;

    public MaiMaiDx()
    {
        _songCatalog = new MaiMaiSongCatalog(Path.Join(ResourceManager.ResourcePath, "SongInfo.json"));
        SongDb = new SongDb<MaiMaiSong>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/MaiMaiSongAliasTemp.txt",
            () => _songCatalog.GetAsync().GetAwaiter().GetResult()
        );

        SongGuessMaker = new SongGuessMaker<MaiMaiSong, MaiMaiDxGuess>(SongDb, song => song.Id <= 100000);
    }

    public void Reset()
    {
        ResetCaches();
        _songCatalog.Invalidate();
        SongDb.Reset();
        _dataFetchers.Clear();
    }

    public SongGuessMaker<MaiMaiSong, MaiMaiDxGuess> SongGuessMaker { get; }


    public SongDb<MaiMaiSong> SongDb { get; }


    public override Task ExceptionHandler(Exception exception, Message message)
    {
        if (CommonExceptionHandler.TryHandleCommonException(exception, message))
        {
            return Task.CompletedTask;
        }

        switch (CommonExceptionHandler.UnwrapCommonException(exception))
        {
            case FlurlHttpException { StatusCode: 400 }:
                message.Reply("“查无此人”");
                break;
            case (FlurlHttpException { StatusCode: 403 }):
                message.Reply("“403 forbidden”");
                break;
            case (FlurlHttpException { StatusCode: 404 }):
                message.Reply("404 Not Found（如果你邦的是Wahlap，那有可能是它的网烂了）");
                break;
            case FlurlHttpTimeoutException:
                message.Reply("Timeout");
                break;
            case FlurlHttpException e:
                message.Reply(e.Message);
                break;
            case HttpRequestException { HttpRequestError: HttpRequestError.Unknown } e:
                message.Reply(e.Message);
                break;
            default:
                return base.ExceptionHandler(exception, message);
        }
        return Task.CompletedTask;
    }
}
