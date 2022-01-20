using System.Dynamic;
using Newtonsoft.Json;
using QQBot.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Plugin.Arcaea;
using QQBot.Plugin.Shared.Arcaea;
using QQBot.Plugin.Shared.Util.SongDb;

namespace QQBot.Plugin.Arcaea;

public partial class Arcaea
{
    private readonly SongDb<ArcaeaSong, ArcaeaGuess> _songDb = new(
        ResourceManager.ResourcePath + "/aliases.tsv",
        ResourceManager.TempPath     + "/ArcaeaSongAliasTemp.txt",
        () =>
        {
            var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
            ) as dynamic[];

            return data.Select(d => new ArcaeaSong(d)).ToList();
        },
        nameof(BotDbContext.ArcaeaGuesses),
        Dialog.AddHandler
    );
}