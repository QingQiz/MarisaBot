using System.Dynamic;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;
using Marisa.Plugin.Shared.Arcaea;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin;

[MarisaPlugin(PluginPriority.Arcaea)]
[MarisaPluginDoc("音游 Arcaea 相关功能")]
[MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "arcaea", "arc", "阿卡伊")]
public class Arcaea :
    MarisaPluginBase,
    IMarisaPluginWithHelp,
    IMarisaPluginWithRetrieve<ArcaeaSong, ArcaeaGuess>,
    IMarisaPluginWithCoverGuess<ArcaeaSong, ArcaeaGuess>
{
    public Arcaea()
    {
        SongDb = new SongDb<ArcaeaSong, ArcaeaGuess>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/ArcaeaSongAliasTemp.txt",
            () =>
            {
                var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                    File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
                ) as dynamic[];

                return data!.Select(d => new ArcaeaSong(d)).ToList();
            },
            nameof(BotDbContext.ArcaeaGuesses),
            Dialog.AddHandler
        );
    }

    public SongDb<ArcaeaSong, ArcaeaGuess> SongDb { get; }
}