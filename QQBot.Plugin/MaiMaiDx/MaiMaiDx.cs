using System.Dynamic;
using Flurl.Http;
using Newtonsoft.Json;
using QQBot.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using QQBot.Plugin.Shared.MaiMaiDx;
using QQBot.Plugin.Shared.Util.SongDb;

namespace QQBot.Plugin.MaiMaiDx;

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
                return data.Select(d => new MaiMaiSong(d)).ToList();
            }
        },
        nameof(BotDbContext.MaiMaiDxGuesses),
        (id, handler) => Dialog.AddHandler(id, null, handler)
    );
}