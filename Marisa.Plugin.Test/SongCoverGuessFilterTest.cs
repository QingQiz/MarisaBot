using System;
using System.Collections.Generic;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Util.SongGuessMaker;
using NUnit.Framework;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Test;

public class SongCoverGuessFilterTest
{
    [TestCase(11363L, 111363L)]
    [TestCase(220L, 100220L)]
    [TestCase(100000L, 100001L)]
    public void MaiMaiFilterExcludesUtageButKeepsTheCatalog(long normalId, long utageId)
    {
        var normal = new ProbeSong(normalId);
        var utage = new ProbeSong(utageId);
        var db = Db(normal, utage);
        var maker = new SongGuessMaker<ProbeSong, MaiMaiDxGuess>(db, song => song.Id <= 100000);

        var selected = Assert.Throws<SelectedSongException>(() => maker.StartSongCoverGuess(null!, 0, 3, null));
        Assert.Multiple(() =>
        {
            Assert.That(selected!.Song, Is.SameAs(normal));
            Assert.That(db.SongList, Has.Count.EqualTo(2));
            Assert.That(db.FindSong(utageId), Is.SameAs(utage));
        });
    }

    [Test]
    public void DefaultAndCallerFiltersAreBothApplied()
    {
        var first = new ProbeSong(220);
        var second = new ProbeSong(11363);
        var utage = new ProbeSong(100220);
        var maker = new SongGuessMaker<ProbeSong, MaiMaiDxGuess>(Db(first, second, utage), song => song.Id <= 100000);

        var selected = Assert.Throws<SelectedSongException>(() =>
            maker.StartSongCoverGuess(null!, 0, 3, song => song != first));
        Assert.That(selected!.Song, Is.SameAs(second));
    }

    [Test]
    public void CallerFilterCannotReintroduceExcludedSongs()
    {
        var maker = new SongGuessMaker<ProbeSong, MaiMaiDxGuess>(Db(new ProbeSong(220), new ProbeSong(100220)),
            song => song.Id <= 100000);
        var queue = new MessageQueueProvider();
        var message = new Message(new MessageSenderProvider(queue), new MessageDataId(1, 1), new MessageDataText(""))
        {
            Type = MessageType.FriendMessage
        };

        Assert.DoesNotThrow(() => maker.StartSongCoverGuess(message, 0, 3, song => song.Id > 100000));
        Assert.That(queue.SendQueue.Reader.TryRead(out var reply), Is.True);
        Assert.That(reply!.MessageChain.Text, Is.EqualTo("None"));
    }

    [Test]
    public void GamesWithoutDefaultFilterKeepHighIdSongs()
    {
        var song = new ProbeSong(100220);
        var maker = new SongGuessMaker<ProbeSong, MaiMaiDxGuess>(Db(song));

        var selected = Assert.Throws<SelectedSongException>(() => maker.StartSongCoverGuess(null!, 0, 3, null));
        Assert.That(selected!.Song, Is.SameAs(song));
    }

    [Test]
    public void CallerFilterStillWorksWithoutDefaultFilter()
    {
        var first = new ProbeSong(220);
        var second = new ProbeSong(100220);
        var maker = new SongGuessMaker<ProbeSong, MaiMaiDxGuess>(Db(first, second));

        var selected = Assert.Throws<SelectedSongException>(() =>
            maker.StartSongCoverGuess(null!, 0, 3, song => song == second));
        Assert.That(selected!.Song, Is.SameAs(second));
    }

    private static SongDb<ProbeSong> Db(params ProbeSong[] songs) =>
        new("unused.tsv", "unused.tmp", () => new List<ProbeSong>(songs));

    private sealed class ProbeSong : Song
    {
        public ProbeSong(long id)
        {
            Id = id;
            Title = $"song-{id}";
        }

        public override string MaxLevel() => throw new NotSupportedException();
        public override string GetImage() => throw new NotSupportedException();
        public override Image GetCover() => throw new SelectedSongException(this);
    }

    private sealed class SelectedSongException(ProbeSong song) : Exception
    {
        public ProbeSong Song { get; } = song;
    }
}
