using System.Dynamic;
using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private readonly SongDb<MaiMaiSong, MaiMaiDxGuess> _songDb = new(
        ResourceManager.ResourcePath + "/aliases.tsv",
        ResourceManager.TempPath     + "/MaiMaiSongAliasTemp.txt",
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
        nameof(BotDbContext.MaiMaiDxGuesses),
        Dialog.AddHandler
    );

    public IEnumerable<MaiMaiSong> SongsMissCover()
    {
        return _songDb.SongList.Where(s =>
        {
            var p = Path.Join(ResourceManager.ResourcePath, "cover", s.Id.ToString());
            return !(File.Exists(p + ".jpg") || File.Exists(p + ".png") || File.Exists(p + ".jpeg"));
        });
    }

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