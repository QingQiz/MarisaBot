using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    #region triggers

    private static MarisaPluginTrigger.PluginTrigger ListBaseTrigger => (message, _) =>
    {
        if (message.Command.StartsWith("b", StringComparison.OrdinalIgnoreCase))
        {
            return !message.Command.StartsWith("bpm", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    };

    #endregion

    #region rating

    private static async Task<DxRating> GetDxRating(string? username, long? qq, bool b50 = false)
    {
        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(b50
            ? string.IsNullOrEmpty(username)
                ? new { qq, b50 }
                : new { username, b50 }
            : string.IsNullOrEmpty(username)
                ? new { qq }
                : new { username });
        return new DxRating(await response.GetJsonAsync(), b50);
    }

    private static async Task<MessageChain> GetB40Card(Message message, bool b50 = false)
    {
        var username = message.Command;
        var qq       = message.Sender!.Id;

        if (string.IsNullOrWhiteSpace(username))
        {
            var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
            if (at != null)
            {
                qq = (at as MessageDataAt)?.Target ?? qq;
            }
        }

        MessageChain ret;
        try
        {
            ret = MessageChain.FromImageB64((await GetDxRating(username, qq, b50)).GetImage());
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            ret = MessageChain.FromText("“查无此人”");
        }
        catch (FlurlHttpException e) when (e.StatusCode == 403)
        {
            ret = MessageChain.FromText("“403 forbidden”");
        }
        catch (FlurlHttpTimeoutException)
        {
            ret = MessageChain.FromText("Timeout");
        }
        catch (FlurlHttpException e)
        {
            ret = MessageChain.FromText(e.Message);
        }

        return ret;
    }

    #endregion

    #region summary

    private async Task<Dictionary<(long Id, long LevelIdx), SongScore>?> GetAllSongScores(
        Message message,
        string[]? versions = null)
    {
        var qq  = message.Sender!.Id;
        var ats = message.At().ToList();

        if (ats.Any())
        {
            qq = ats.First();
        }

        try
        {
            var response = await "https://www.diving-fish.com/api/maimaidxprober/query/plate".PostJsonAsync(new
            {
                qq, version = versions ?? MaiMaiSong.Plates
            });

            var verList = ((await response.GetJsonAsync())!.verlist as List<object>)!;

            return verList.Select(data =>
            {
                var d    = data as dynamic;
                var song = (_songDb.FindSong(d.id) as MaiMaiSong)!;
                var idx  = (int)d.level_index;

                var ach      = d.achievements;
                var constant = song.Constants[idx];

                return new SongScore(ach, constant, -1, d.fc, d.fs, d.level, idx, MaiMaiSong.LevelName[idx],
                    SongScore.Ra(ach, constant), SongScore.CalcRank(ach), song.Id, song.Title, song.Type);
            }).ToDictionary(ss => (ss.Id, ss.LevelIdx));
        }
        catch (FlurlHttpException e) when (e.StatusCode == 404)
        {
            message.Reply("NotFound");
            return null;
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            message.Reply("400");
            return null;
        }
        catch (FlurlHttpException e) when (e.StatusCode == 403)
        {
            message.Reply("Forbidden");
            return null;
        }
        catch (FlurlHttpTimeoutException)
        {
            message.Reply("Timeout");
            return null;
        }
        catch (FlurlHttpException e)
        {
            message.Reply(e.Message);
            return null;
        }
    }


    #endregion

    #region Select Song

    /// <summary>
    /// 从多个筛选结果中随机选一个
    /// </summary>
    /// <param name="songs"></param>
    /// <param name="message"></param>
    private static void RandomSelectResult(List<MaiMaiSong> songs, Message message)
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
    /// <param name="songs"></param>
    /// <param name="message"></param>
    private static void MultiPageSelectResult(IReadOnlyList<MaiMaiSong> songs, Message message)
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

        Dialog.AddHandler(message.GroupInfo?.Id, message.Sender?.Id, next =>
        {
            if (next.Command.StartsWith("p", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(next.Command[1..], out var p))
                {
                    message.Reply(DisplaySong(p));
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }
            }

            return Task.FromResult(MarisaPluginTaskState.Canceled);
        });
    }

    private List<MaiMaiSong> SelectSongByBaseRange(string baseRange)
    {
        if (baseRange.Contains('-'))
        {
            if (double.TryParse(baseRange.Split('-')[0], out var base1) &&
                double.TryParse(baseRange.Split('-')[1], out var base2))
            {
                return _songDb.SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
            }
        }
        else
        {
            if (double.TryParse(baseRange, out var @base))
            {
                return _songDb.SongList.Where(s => s.Constants.Contains(@base)).ToList();
            }
        }

        return new List<MaiMaiSong>();
    }

    private List<MaiMaiSong> SelectSongByCharter(string charter)
    {
        return _songDb.SongList
            .Where(s => s.Charters.Any(c => c.Contains(charter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private List<MaiMaiSong> SelectSongByLevel(string lv)
    {
        return _songDb.SongList.Where(s => s.Levels.Contains(lv)).ToList();
    }

    private List<MaiMaiSong> SelectSongByBpmRange(string bpm)
    {
        if (bpm.Contains('-'))
        {
            if (long.TryParse(bpm.Split('-')[0], out var bpm1) &&
                long.TryParse(bpm.Split('-')[1], out var bpm2))
            {
                return _songDb.SongList.Where(s => s.Info.Bpm >= bpm1 && s.Info.Bpm <= bpm2).ToList();
            }
        }
        else
        {
            if (long.TryParse(bpm, out var bpmOut))
            {
                return _songDb.SongList.Where(s => s.Info.Bpm == bpmOut).ToList();
            }
        }

        return new List<MaiMaiSong>();
    }

    private List<MaiMaiSong> SelectSongByArtist(string artist)
    {
        var regex = new Regex($@"\b{artist}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 全字搜索
        var res = _songDb.SongList.Where(s => regex.IsMatch(s.Artist)).ToList();

        return res.Any()
            ? res.ToList()
            : _songDb.SongList.Where(
                s => s.Info.Artist.Contains(artist, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private List<MaiMaiSong> SelectSongWhenNew()
    {
        return _songDb.SongList.Where(s => s.Info.IsNew).ToList();
    }

    private List<MaiMaiSong> SelectSongWhenOld()
    {
        return _songDb.SongList.Where(s => !s.Info.IsNew).ToList();
    }

    #endregion
}