using System.Dynamic;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;
using Marisa.Plugin.Shared.Arcaea;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Util.SongGuessMaker;
using Newtonsoft.Json;

namespace Marisa.Plugin;

[MarisaPlugin(PluginPriority.Arcaea)]
[MarisaPluginDoc("音游 Arcaea 相关功能")]
[MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "arcaea", "arc", "阿卡伊")]
public class Arcaea :
    MarisaPluginBase,
    IMarisaPluginWithHelp,
    IMarisaPluginWithRetrieve<ArcaeaSong>,
    IMarisaPluginWithCoverGuess<ArcaeaSong, ArcaeaGuess>
{
    public Arcaea()
    {
        SongDb = new SongDb<ArcaeaSong>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/ArcaeaSongAliasTemp.txt",
            SongListGen,
            Dialog.TryAddHandler
        );

        SongGuessMaker = new SongGuessMaker<ArcaeaSong, ArcaeaGuess>(SongDb, nameof(BotDbContext.ArcaeaGuesses));
    }

    public SongGuessMaker<ArcaeaSong, ArcaeaGuess> SongGuessMaker { get; }

    public SongDb<ArcaeaSong> SongDb { get; }

    public static List<ArcaeaSong> SongListGen()
    {
        var data = JsonConvert.DeserializeObject<ExpandoObject[]>(File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")) as dynamic[];

        return data!.Select(d => new ArcaeaSong(d)).ToList();
    }
}