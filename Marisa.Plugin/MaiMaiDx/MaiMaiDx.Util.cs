using Flurl.Http;
using Marisa.Plugin.Shared.MaiMaiDx;
using osu.Game.Extensions;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    #region triggers

    public static MarisaPluginTrigger.PluginTrigger ListBaseTrigger => (message, _) =>
    {
        if (message.Command.StartsWith("b", StringComparison.OrdinalIgnoreCase))
        {
            return !message.Command.StartsWith("bpm", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    };

    #endregion

    #region rating

    private (Dictionary<(long Id, int Idx), double> Up, Dictionary<(long Id, int Idx), double> Down) GetRecommend(DxRating rating, int targetRating)
    {
        var listOld = rating.OldScores.Select(score => (_songDb.GetSongById(score.Id)!, score.LevelIdx, score.Achievement)).ToList();
        var listNew = rating.NewScores.Select(score => (_songDb.GetSongById(score.Id)!, score.LevelIdx, score.Achievement)).ToList();

        var raOld = rating.OldScores.Sum(s => s.Ra());
        var raNew = rating.NewScores.Sum(s => s.Ra());

        var idSet = new HashSet<(long, int)>();
        idSet.AddRange(rating.OldScores.Select(s => (s.Id, s.LevelIdx)));
        idSet.AddRange(rating.NewScores.Select(s => (s.Id, s.LevelIdx)));

        var up   = new Dictionary<(long Id, int Idx), double>();
        var down = new Dictionary<(long Id, int Idx), double>();

        var newSongList = _songDb.SongList.Where(s => s.Info.IsNew).ToList();
        var oldSongList = _songDb.SongList.Where(s => !s.Info.IsNew).ToList();

        var maxConst = Math.Max(rating.NewScores.Max(x => x.Constant), rating.OldScores.Max(x => x.Constant));

        while (true)
        {
            if (raOld + raNew >= targetRating) break;

            var successOld = UpdateRa(listOld, ref raOld, oldSongList);

            if (raOld + raNew >= targetRating) break;

            var successNew = UpdateRa(listNew, ref raNew, newSongList);

            if (!successOld && !successNew) return (new Dictionary<(long Id, int Idx), double>(), new Dictionary<(long Id, int Idx), double>());
        }

        return (up, down);

        bool UpdateRa(IList<(MaiMaiSong Song, int Idx, double Achievement)> list, ref int raSum, IEnumerable<MaiMaiSong> songList)
        {
            var (oldSong, oldIdx, oldAchievement) = list.MinBy(x => x.Song.Ra(x.Idx, x.Achievement));

            var oldRa = oldSong.Ra(oldIdx, oldAchievement);

            var oldKey = (oldSong.Id, oldIdx);

            idSet.Remove(oldKey);

            var @const = maxConst;
            var songs = songList
                .Where(s => s.Constants
                    .Select((c, i) => (c, i))
                    // 你妈逼的浮点误差，14.7 -> 14.699999999999999
                    .Any(c =>
                        !idSet.Contains((s.Id, c.i))
                     && c.c <= @const + 0.15
                     && SongScore.Ra(100.5, c.c) > oldRa
                    )
                )
                .ToList();

            if (!songs.Any())
            {
                // revert change
                idSet.Add(oldKey);

                var items = list.Select((x, i) => (x, i)).Where(x => x.x.Achievement < 100.5).ToList();

                if (!items.Any()) return false;

                var (item, idx) = items.RandomTake();

                var ra          = SongScore.Ra(item.Achievement, item.Song.Constants[item.Idx]);
                var achievement = SongScore.NextAchievement(item.Song.Constants[item.Idx], ra);

                raSum -= ra;
                raSum += SongScore.Ra(achievement, item.Song.Constants[item.Idx]);

                list[idx] = (item.Song, item.Idx, achievement);

                if (!down.ContainsKey((item.Song.Id, item.Idx)))
                {
                    down.Add((item.Song.Id, item.Idx), item.Achievement);
                }

                up[(item.Song.Id, item.Idx)] = achievement;

                return true;
            }

            if (!down.ContainsKey(oldKey))
            {
                if (!up.Remove(oldKey)) down.Add(oldKey, oldAchievement);
            }

            var newSong = songs.RandomTake();

            var achievements = newSong.Constants.Select(c => SongScore.NextAchievement(c, oldRa)).ToList();
            var newIdx = achievements
                .Select((x, i) => (x, i)).ToList()
                .FindIndex(x => !idSet.Contains((newSong.Id, x.i)) && x.x > 0);
            var newAchievement = achievements[newIdx];
            var newKey         = (newSong.Id, newIdx);

            var newRa = SongScore.Ra(newAchievement, newSong.Constants[newIdx]);

            raSum -= oldRa;
            raSum += newRa;

            maxConst = Math.Max(maxConst, newSong.Constants[newIdx]);

            up[newKey] = newAchievement;
            idSet.Add(newKey);
            list.Remove((oldSong, oldIdx, oldAchievement));
            list.Add((newSong, newIdx, newAchievement));
            return true;
        }
    }

    private static async Task<IFlurlResponse> B50Request(string? username, long? qq)
    {
        return await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(
            string.IsNullOrEmpty(username)
                ? new { qq, b50 = true }
                : new { username, b50 = true });
    }

    private static async Task<DxRating> GetDxRating(string? username, long? qq)
    {
        var rating = await B50Request(username, qq);

        return new DxRating(await rating.GetJsonAsync());
    }

    private static async Task<MessageChain> GetB40Card(Message message)
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

        var b50 = await B50Request(username, qq);

        var context = new WebContext();

        context.Put("b50", await b50.GetStringAsync());

        return MessageChain.FromImageB64(await WebApi.MaiMaiBest(context.Id));
    }

    #endregion

    #region summary

    private async Task<Dictionary<(long Id, int LevelIdx), SongScore>?> GetAllSongScores(
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