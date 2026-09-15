using System.Dynamic;
using Flurl.Http;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
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
    IHandleCommonException,
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
            }
        );

        SongGuessMaker = new SongGuessMaker<MaiMaiSong, MaiMaiDxGuess>(SongDb);
    }

    public void Reset()
    {
        ResetCaches();
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
                message.Reply("查不到这个账号。");
                break;
            case (FlurlHttpException { StatusCode: 403 }):
                message.Reply("查分器拒绝了请求（403）。");
                break;
            case (FlurlHttpException { StatusCode: 404 }):
                message.Reply("查分器返回 404。若绑定的是 Wahlap，可能是服务暂时不可用。");
                break;
            case FlurlHttpTimeoutException:
                message.Reply("请求超时，请稍后再试。");
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
