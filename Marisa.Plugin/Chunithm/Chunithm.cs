using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;
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

        SongGuessMaker = new SongGuessMaker<ChunithmSong, ChunithmGuess>(SongDb, nameof(BotDbContext.ChunithmGuesses));
    }

    public void Reset()
    {
        SongDb.Reset();
        new DivingFishDataFetcher(SongDb).Reset();
        new LouisDataFetcher(SongDb).Reset();
    }

    public SongGuessMaker<ChunithmSong, ChunithmGuess> SongGuessMaker { get; }

    public SongDb<ChunithmSong> SongDb { get; }

    public override Task ExceptionHandler(Exception exception, Message message)
    {
        switch (exception)
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
                base.ExceptionHandler(exception, message);
                break;
        }
        return Task.CompletedTask;
    }
}