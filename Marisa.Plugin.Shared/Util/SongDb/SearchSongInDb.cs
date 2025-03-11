using System.Text.RegularExpressions;
using Marisa.Plugin.Shared.Dialog;

namespace Marisa.Plugin.Shared.Util.SongDb;

public static class SearchSongInDb
{
    public static async Task<MarisaPluginTaskState> SearchSong<T>(this SongDb<T> songDb, Message message) where T : Song
    {
        _ = await MultiPageSelectResult(songDb, songDb.SearchSong(message.Command), message);

        return MarisaPluginTaskState.CompletedTask;
    }

    #region Select Song

    /// <summary>
    ///     从多个筛选结果中随机选一个
    /// </summary>
    public static void RandomSelectResult<T>(this List<T> songs, Message message) where T : Song
    {
        if (songs.Count == 0)
        {
            message.Reply("EMPTY");
            return;
        }

        message.Reply(MessageDataImage.FromBase64(songs.RandomTake().GetImage()));
    }

    /// <summary>
    /// 分页展示搜索结果
    /// </summary>
    /// <param name="db"></param>
    /// <param name="songs"></param>
    /// <param name="message"></param>
    /// <param name="reply">是否回复歌曲详细信息（图片）</param>
    /// <typeparam name="T">TSong</typeparam>
    /// <returns></returns>
    public static Task<T?> MultiPageSelectResult<T>(this SongDb<T> db, IReadOnlyList<T> songs, Message message, bool reply = true) where T : Song
    {
        var result = new TaskCompletionSource<T?>();
        switch (songs.Count)
        {
            case 0:
                result.SetResult(null);
                message.Reply("“查无此歌”");
                return result.Task;
            case 1:
                result.SetResult(songs[0]);
                if (reply) message.Reply(new MessageDataText(songs[0].Title), MessageDataImage.FromBase64(songs[0].GetImage()));
                return result.Task;
        }

        message.Reply(DisplaySong(0));

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            if (!next.IsPlainText())
            {
                result.SetResult(null);
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            if (next.Command.StartsWith("p", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(next.Command[1..].Span, out var p))
                {
                    message.Reply(DisplaySong(p));
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }
            }

            if (long.TryParse(next.Command.Span, out var id))
            {
                var song = db.GetSongById(id);

                if (song == null)
                {
                    message.Reply("查无此歌");
                    result.SetResult(null);
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                }
                if (reply) message.Reply(new MessageDataText(song.Title), MessageDataImage.FromBase64(song.GetImage()));
                result.SetResult(song);
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            result.SetResult(null);
            return Task.FromResult(MarisaPluginTaskState.Canceled);
        });
        return result.Task;

        string DisplaySong(int page)
        {
            var p = Math.Max(0, page - 1);
            var ret = string.Join('\n',
                songs
                    .Skip(p * SongDbConfig.PageSize)
                    .Take(SongDbConfig.PageSize)
                    .OrderBy(x => x.Id)
                    .Select(song => $"[ID:{song.Id}, Lv:{song.MaxLevel()}] -> {song.Title}"));

            if (songs.Count > SongDbConfig.PageSize)
            {
                var pageAll = (songs.Count + SongDbConfig.PageSize - 1) / SongDbConfig.PageSize;
                ret += $"\n一共有 {songs.Count} 个结果，当前页 {p + 1}/{pageAll}\n发送 p1、p2 等进行换页";
            }

            ret += "\n发送歌曲 id 进一步选择";
            return ret;
        }
    }

    public static List<T> SelectSongByBaseRange<T>(this SongDb<T> db, ReadOnlyMemory<char> baseRange) where T : Song
    {
        if (baseRange.Span.IndexOf('-') != -1)
        {
            var range = baseRange.Split('-').ToArray();
            if (double.TryParse(range[0].Span, out var base1) &&
                double.TryParse(range[1].Span, out var base2))
            {
                return db.SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
            }
        }
        else
        {
            if (double.TryParse(baseRange.Span, out var @base))
            {
                return db.SongList.Where(s => s.Constants.Contains(@base)).ToList();
            }
        }

        return [];
    }

    public static List<T> SelectSongByCharter<T>(this SongDb<T> db, ReadOnlyMemory<char> charter) where T : Song
    {
        return db.SongList
            .Where(s => s.Charters.Any(c => c.Contains(charter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static List<T> SelectSongByLevel<T>(this SongDb<T> db, ReadOnlyMemory<char> lv) where T : Song
    {
        return db.SongList.Where(s => s.Levels.Any(l => l.Equals(lv, StringComparison.Ordinal))).ToList();
    }

    public static List<T> SelectSongByBpmRange<T>(this SongDb<T> db, ReadOnlyMemory<char> bpm) where T : Song
    {
        if (bpm.Span.Contains('-'))
        {
            var range = bpm.Split('-').ToArray();

            if (double.TryParse(range[0].Span, out var bpm1) &&
                double.TryParse(range[1].Span, out var bpm2))
            {
                return db.SongList.Where(s => s.Bpm >= bpm1 && s.Bpm <= bpm2).ToList();
            }
        }
        else
        {
            if (long.TryParse(bpm.Span, out var bpmOut))
            {
                return db.SongList.Where(s => Math.Abs(s.Bpm - bpmOut) < 0.2).ToList();
            }
        }

        return [];
    }

    public static List<T> SelectSongByArtist<T>(this SongDb<T> db, ReadOnlyMemory<char> artist) where T : Song
    {
        try
        {
            var regex = new Regex(artist.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var res = db.SongList.Where(s => regex.IsMatch(s.Artist)).ToList();

            if (res.Count != 0)
                return res.ToList();
        }
        catch (RegexParseException)
        {
        }

        return db.SongList
            .Where(s =>
                s.Artist.Contains(artist, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    #endregion
}