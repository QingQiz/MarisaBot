using Flurl.Http;
using Marisa.Plugin.Shared.MaiMaiDx;

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
            ret = MessageChain.FromImageB64(
                MaiMaiDraw.DrawB40Card(await GetDxRating(username, qq, b50)).ToB64()
            );
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