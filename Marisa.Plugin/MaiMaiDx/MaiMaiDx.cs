using System.Dynamic;
using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Util.SongGuessMaker;
using Newtonsoft.Json;

namespace Marisa.Plugin.MaiMaiDx;

[MarisaPluginDoc("音游 maimai DX 的相关功能")]
[MarisaPlugin(PluginPriority.MaiMaiDx)]
[MarisaPluginCommand("maimai", "mai", "舞萌")]
public partial class MaiMaiDx :
    MarisaPluginBase,
    ICanReset,
    IMarisaPluginWithHelp,
    IMarisaPluginWithRetrieve<MaiMaiSong>,
    IMarisaPluginWithCoverGuess<MaiMaiSong, MaiMaiDxGuess>

{
    public MaiMaiDx()
    {
        SongDb = new SongDb<MaiMaiSong>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/MaiMaiSongAliasTemp.txt",
            () =>
            {
                try
                {
                    var data = "https://www.diving-fish.com/api/maimaidxprober/music_data".GetJsonListAsync().Result;

                    return data.Select(d => new MaiMaiSong(d)).ToList();
                }
                catch
                {
                    var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                        File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
                    ) as dynamic[];
                    return data!.Select(d => new MaiMaiSong(d)).ToList();
                }
            },
            Dialog.TryAddHandler
        );

        SongGuessMaker = new SongGuessMaker<MaiMaiSong, MaiMaiDxGuess>(SongDb, nameof(BotDbContext.MaiMaiDxGuesses));
    }

    public void Reset()
    {
        SongDb.Reset();
        _dataFetchers.Clear();
    }

    public SongGuessMaker<MaiMaiSong, MaiMaiDxGuess> SongGuessMaker { get; }


    public SongDb<MaiMaiSong> SongDb { get; }


    public override Task ExceptionHandler(Exception exception, Message message)
    {
        switch (exception)
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
            default:
                base.ExceptionHandler(exception, message);
                break;
        }
        return Task.CompletedTask;
    }
}