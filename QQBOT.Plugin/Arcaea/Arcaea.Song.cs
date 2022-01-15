using System.Dynamic;
using Newtonsoft.Json;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.Plugin.Shared.Arcaea;
using QQBot.Plugin.Shared.Util.SongDb;

namespace QQBot.Plugin.Arcaea;

public partial class Arcaea
{
    private readonly SongDb<ArcaeaSong> _songDb = new(
        ResourceManager.ResourcePath + "/aliases.tsv",
        ResourceManager.TempPath     + "/ArcaeaSongAliasTemp.txt",
        () =>
        {
            var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
            ) as dynamic[];

            return data.Select(d => new ArcaeaSong(d)).ToList();
        });
   
    private static MessageChain GetSearchResult(IReadOnlyList<ArcaeaSong> songs)
    {
        return songs.Count switch
        {
            >= 10 => MessageChain.FromPlainText($"过多的结果（{songs.Count}个）"),
            0     => MessageChain.FromPlainText("“查无此歌”"),
            1 => new MessageChain(new MessageData[]
            {
                new PlainMessage(songs[0].Title),
                ImageMessage.FromBase64(songs[0].GetImage())
            }),
            _ => MessageChain.FromPlainText(string.Join('\n',
                songs.Select(song => $"[ID:{song.Id}] -> {song.Title}")))
        };
    }
}