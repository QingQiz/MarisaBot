﻿using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util;
using osu.Game.Extensions;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    #region recommend

    private (List<(MaiMaiSong, int, double, int)> listOld, List<(MaiMaiSong, int, double, int)> listNew, bool)
        GetRecommend(DxRating rating, int targetRating)
    {
        var listOld = rating.OldScores
            .Select(score => (SongDb.GetSongById(score.Id)!, score.LevelIdx, score.Achievement, score.Rating))
            .ToList();
        var listNew = rating.NewScores
            .Select(score => (SongDb.GetSongById(score.Id)!, score.LevelIdx, score.Achievement, score.Rating))
            .ToList();

        var raOld = rating.OldScores.Sum(s => s.Ra());
        var raNew = rating.NewScores.Sum(s => s.Ra());

        var idSet = new HashSet<(long, int)>();
        idSet.AddRange(rating.OldScores.Select(s => (s.Id, s.LevelIdx)));
        idSet.AddRange(rating.NewScores.Select(s => (s.Id, s.LevelIdx)));

        var newSongList = SongDb.SongList.Where(s => s.Info.IsNew).ToList();
        var oldSongList = SongDb.SongList.Where(s => !s.Info.IsNew).ToList();

        double maxConst = 0;

        try
        {
            maxConst = Math.Max(rating.NewScores.Max(x => x.Constant), rating.OldScores.Max(x => x.Constant));
        }
        catch (InvalidOperationException)
        {
        }

        var fails = 0b1111;
        var rand  = new Random();
        while (true)
        {
            if (raOld + raNew >= targetRating) break;
            if (fails == 0) break;

            int r;
            do
            {
                r = rand.Next(4);
            } while ((1 << r & fails) == 0);

            switch (r)
            {
                case 0:
                {
                    var success = Update1(listOld, ref raOld, oldSongList, 35);

                    if (!success)
                        fails &= 0b1110;
                    else
                        fails |= 0b0100;
                    break;
                }
                case 1:
                {
                    var success = Update1(listNew, ref raNew, newSongList, 15);

                    if (!success)
                        fails &= 0b1101;
                    else
                        fails |= 0b1000;
                    break;
                }
                case 2:
                {
                    var success = Update2(listOld, ref raOld);

                    if (!success) fails &= 0b1011;
                    break;
                }
                case 3:
                {
                    var success = Update2(listNew, ref raNew);

                    if (!success) fails &= 0b0111;
                    break;
                }
            }
        }

        return (listOld, listNew, fails != 0);

        List<MaiMaiSong> GetAvailableSongs(IEnumerable<MaiMaiSong> songs, int minRa)
        {
            return songs
                .Where(s => s.Constants
                    .Select((c, i) => (c, i))
                    // 你妈逼的浮点误差，14.7 -> 14.699999999999999
                    .Any(c =>
                        !idSet.Contains((s.Id, c.i))
                     && c.c <= maxConst + 0.15
                     && SongScore.Ra(100.5, c.c) > minRa
                    )
                )
                .ToList();
        }

        bool Update1(IList<(MaiMaiSong Song, int Idx, double Achievement, int Rating)> list, ref int raSum,
            IEnumerable<MaiMaiSong> songList, int cap)
        {
            var (oldSong, oldIdx, oldAchievement, oldRa) = list.MinBy(x => x.Rating);

            idSet.Remove((oldSong.Id, oldIdx));

            var songs = GetAvailableSongs(songList, oldRa);

            if (songs.Count == 0)
            {
                // revert change
                idSet.Add((oldSong.Id, oldIdx));

                return Update2(list, ref raSum);
            }

            var newSong = songs.RandomTake();

            var achievements = newSong.Constants.Select(c => SongScore.NextAchievement(c, oldRa)).ToList();
            var newIdx = achievements
                .Select((x, i) => (x, i)).ToList()
                .FindIndex(x => !idSet.Contains((newSong.Id, x.i)) && x.x > 0);
            var newAchievement = achievements[newIdx];
            var newRa          = SongScore.Ra(newAchievement, newSong.Constants[newIdx]);

            raSum -= oldRa;
            raSum += newRa;

            maxConst = Math.Max(maxConst, newSong.Constants[newIdx]);

            idSet.Add((newSong.Id, newIdx));
            if (list.Count >= cap) list.Remove((oldSong, oldIdx, oldAchievement, oldRa));
            list.Add((newSong, newIdx, newAchievement, newRa));
            return true;
        }

        bool Update2(IList<(MaiMaiSong Song, int Idx, double Achievement, int Rating)> list, ref int raSum)
        {
            var items = list.Select((x, i) => (x, i)).Where(x => x.x.Achievement < 100.5).ToList();

            if (!items.Any()) return false;

            var (item, idx) = items.RandomTake();

            var oldRa          = SongScore.Ra(item.Achievement, item.Song.Constants[item.Idx]);
            var newAchievement = SongScore.NextAchievement(item.Song.Constants[item.Idx], oldRa);
            var newRa          = SongScore.Ra(newAchievement, item.Song.Constants[item.Idx]);

            raSum -= oldRa;
            raSum += newRa;

            list[idx] = (item.Song, item.Idx, newAchievement, newRa);

            return true;
        }
    }

    #endregion

    #region data fetcher

    private DataFetcher GetDataFetcher(Message message, bool allowUsername = false)
    {
        // Command不为空的话，就是用用户名查。只有DivingFish能使用用户名查
        if (allowUsername && !message.Command.IsWhiteSpace())
        {
            return GetDataFetcher(DataFetcherType.DivingFish);
        }

        var qq = message.Sender.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        using var db = new BotDbContext();

        var bind = db.MaiMaiBinds.FirstOrDefault(x => x.UId == qq);

        return GetDataFetcher(bind == null ? DataFetcherType.DivingFish : DataFetcherType.Wahlap);
    }

    private readonly Dictionary<DataFetcherType, DataFetcher> _dataFetchers = new();

    private DataFetcher GetDataFetcher(DataFetcherType type)
    {
        if (_dataFetchers.TryGetValue(type, out var fetcher)) return fetcher;

        return _dataFetchers[type] = type switch
        {
            DataFetcherType.DivingFish => new DivingFishDataFetcher(SongDb),
            DataFetcherType.Wahlap     => new AllNetDataFetcher(SongDb),
            _                          => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private enum DataFetcherType
    {
        DivingFish,
        Wahlap
    }

    #endregion
}