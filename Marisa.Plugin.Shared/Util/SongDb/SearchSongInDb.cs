using System.Text.RegularExpressions;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Marisa.Utils;

namespace Marisa.Plugin.Shared.Util.SongDb;

public static class SearchSongInDb
{
    public static MarisaPluginTaskState SearchSong<T, TSongGuess>(this SongDb<T, TSongGuess> songDb, Message message)
        where T : Song where TSongGuess : SongGuess, new()
    {
        var search = songDb.SearchSong(message.Command);

        message.Reply(songDb.GetSearchResult(search));

        if (search.Count is > 1 and < SongDbConfig.PageSize)
        {
            songDb.MessageHandlerAdder(message.GroupInfo?.Id, message.Sender?.Id, hMessage =>
            {
                // 不是纯文本
                if (!hMessage.IsPlainText())
                {
                    return Task.FromResult(MarisaPluginTaskState.Canceled);
                }

                // 不是 id
                if (!long.TryParse(hMessage.Command.Trim(), out var songId))
                {
                    return Task.FromResult(MarisaPluginTaskState.Canceled);
                }

                var song = songDb.GetSongById(songId);
                // 没找到歌
                if (song == null)
                {
                    return Task.FromResult(MarisaPluginTaskState.Canceled);
                }

                message.Reply(songDb.GetSearchResult(new[] { song }));
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            });
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #region Select Song

    /// <summary>
    /// 从多个筛选结果中随机选一个
    /// </summary>
    public static void RandomSelectResult<T>(this List<T> songs, Message message) where T : Song
    {
        if (!songs.Any())
        {
            message.Reply("EMPTY");
        }

        message.Reply(MessageDataImage.FromBase64(songs.RandomTake().GetImage()));
    }

    /// <summary>
    /// 分页展示多个结果
    /// </summary>
    public static void MultiPageSelectResult<T, TG>(this SongDb<T, TG> db, IReadOnlyList<T> songs, Message message) where T : Song where TG : SongGuess, new()
    {
        string DisplaySong(int page)
        {
            var p = Math.Max(0, page - 1);
            var ret = string.Join('\n',
                songs
                    .Skip(p * SongDbConfig.PageSize)
                    .Take(SongDbConfig.PageSize)
                    .OrderBy(x => x.Id)
                    .Select(song => $"[ID:{song.Id}, Lv:{song.MaxLevel()}] -> {song.Title}"));

            if (songs.Count <= SongDbConfig.PageSize) return ret;

            var pageAll = (songs.Count + SongDbConfig.PageSize - 1) / SongDbConfig.PageSize;
            ret += "\n" + $"一共有 {songs.Count} 个结果，当前页 {p + 1}/{pageAll}";

            return ret;
        }

        switch (songs.Count)
        {
            case 0:
                message.Reply("“查无此歌”");
                return;
            case 1:
                message.Reply(new MessageDataText(songs[0].Title), MessageDataImage.FromBase64(songs[0].GetImage()));
                return;
        }

        message.Reply(DisplaySong(0));

        if (songs.Count <= SongDbConfig.PageSize)
        {
            return;
        }

        db.MessageHandlerAdder(message.GroupInfo?.Id, message.Sender?.Id, next =>
        {
            if (next.Command.StartsWith("p", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(next.Command[1..], out var p))
                {
                    message.Reply(DisplaySong(p));
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }
            }

            if (long.TryParse(next.Command, out var id))
            {
                var song = db.GetSongById(id);

                if (song == null)
                {
                    message.Reply("查无此歌");
                }
                else
                {
                    message.Reply(new MessageDataText(song.Title), MessageDataImage.FromBase64(song.GetImage()));
                }
            }

            return Task.FromResult(MarisaPluginTaskState.Canceled);
        });
    }

    public static List<T> SelectSongByBaseRange<T, TG>(this SongDb<T, TG> db, string baseRange) where T : Song where TG : SongGuess, new()
    {
        if (baseRange.Contains('-'))
        {
            if (double.TryParse(baseRange.Split('-')[0], out var base1) &&
                double.TryParse(baseRange.Split('-')[1], out var base2))
            {
                return db.SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
            }
        }
        else
        {
            if (double.TryParse(baseRange, out var @base))
            {
                return db.SongList.Where(s => s.Constants.Contains(@base)).ToList();
            }
        }

        return new List<T>();
    }

    public static List<T> SelectSongByCharter<T, TG>(this SongDb<T, TG> db, string charter) where T : Song where TG : SongGuess, new()
    {
        return db.SongList
            .Where(s => s.Charters.Any(c => c.Contains(charter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static List<T> SelectSongByLevel<T, TG>(this SongDb<T, TG> db, string lv) where TG : SongGuess, new() where T : Song
    {
        return db.SongList.Where(s => s.Levels.Contains(lv)).ToList();
    }

    public static List<T> SelectSongByBpmRange<T, TG>(this SongDb<T, TG> db, string bpm) where T : Song where TG : SongGuess, new()
    {
        if (bpm.Contains('-'))
        {
            if (long.TryParse(bpm.Split('-')[0], out var bpm1) &&
                long.TryParse(bpm.Split('-')[1], out var bpm2))
            {
                return db.SongList.Where(s => s.Bpm >= bpm1 && s.Bpm <= bpm2).ToList();
            }
        }
        else
        {
            if (long.TryParse(bpm, out var bpmOut))
            {
                return db.SongList.Where(s => Math.Abs(s.Bpm - bpmOut) < 0.2).ToList();
            }
        }

        return new List<T>();
    }

    public static List<T> SelectSongByArtist<T, TG>(this SongDb<T, TG> db, string artist) where T : Song where TG : SongGuess, new()
    {
        var regex = new Regex($@"\b{artist}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 全字搜索
        var res = db.SongList.Where(s => regex.IsMatch(s.Artist)).ToList();

        return res.Any()
            ? res.ToList()
            : db.SongList.Where(
                s => s.Artist.Contains(artist, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    #endregion
}