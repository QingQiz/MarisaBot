using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using Flurl.Http;
using Marisa.Database.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Util.SongGuessMaker;
using Newtonsoft.Json;

namespace Marisa.Plugin.Chunithm;

[MarisaPlugin(PluginPriority.Chunithm)]
[MarisaPluginDoc("音游 Chunithm 的相关功能")]
[MarisaPluginCommand("chunithm", "chu", "中二")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class Chunithm :
    MarisaPluginBase,
    IHandleCommonException,
    ICanReset,
    IMarisaPluginWithHelp,
    IMarisaPluginWithRetrieve<ChunithmSong>,
    IMarisaPluginWithCoverGuess<ChunithmSong, ChunithmGuess>
{
    public Chunithm()
    {
        SongDb = new SongDb<ChunithmSong>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/ChunithmSongAliasTemp.txt",
            () =>
            {
                var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                    File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
                ) as dynamic[];
                return data!.Select(d => new ChunithmSong(d)).ToList();
            }
        );

        SongGuessMaker = new SongGuessMaker<ChunithmSong, ChunithmGuess>(SongDb);
    }

    public void Reset()
    {
        SongDb.Reset();
        // 共享歌单缓存是 LxnsDataFetcher 的 static _songList（DivingFish 也走它），必须清；
        // DivingFish 自己的 _songTitleIndexer 是实例字段、handler 每次请求都新建 fetcher，
        // 对临时实例调 Reset 没有意义，故不再调用它。
        new LxnsDataFetcher(SongDb).Reset();
        new LouisDataFetcher(SongDb).Reset();
    }

    public SongGuessMaker<ChunithmSong, ChunithmGuess> SongGuessMaker { get; }

    public SongDb<ChunithmSong> SongDb { get; }

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
            case FlurlHttpException { StatusCode: 403 }:
                message.Reply("“403 forbidden”");
                break;
            case FlurlHttpException { StatusCode: 404 }:
                message.Reply("404 Not Found");
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
