using System.Dynamic;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;
using Marisa.Plugin.Shared.Arcaea;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin.Arcaea;

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
        (id, handler) => Dialog.AddHandler(id, null, handler)
    );
}