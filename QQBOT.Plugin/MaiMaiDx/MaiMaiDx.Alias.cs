using System.Dynamic;
using Flurl.Http;
using Newtonsoft.Json;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.Plugin.Shared.MaiMaiDx;
using QQBot.Plugin.Shared.Util.SongDb;

namespace QQBot.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private readonly SongDb<MaiMaiSong> _songDb = new(
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
        });

    private static MessageChain GetSearchResult(IReadOnlyList<MaiMaiSong> songs)
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
                songs.Select(song => $"[T:{song.Type}, ID:{song.Id}] -> {song.Title}")))
        };
    }
}