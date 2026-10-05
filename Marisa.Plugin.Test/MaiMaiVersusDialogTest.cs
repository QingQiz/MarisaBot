using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiVersusDialogTest
{
    private static long _nextSender = 800000;

    [Test]
    public async Task NavigatesValidPagesAndSilentlyDropsNextNormalMessage()
    {
        var session = new Session(41);
        await session.Start();
        Assert.That(session.Rendered, Is.EqualTo(new[] { 1 }));
        Assert.That(await session.Send("p3"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(await session.Send("p1"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(await session.Send("P2"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(session.Rendered, Is.EqualTo(new[] { 1, 3, 2 }));
        Assert.That(await session.Send("下一条普通消息"), Is.EqualTo(MarisaPluginTaskState.Canceled));
        Assert.That(DialogManager.ContainsDialog(session.Key), Is.False);
        Assert.That(session.TextReplies(), Is.Empty);
    }

    [Test]
    public async Task SinglePageDoesNotInstallDialog()
    {
        var session = new Session(20);
        await session.Start();
        Assert.That(session.Rendered, Is.EqualTo(new[] { 1 }));
        Assert.That(DialogManager.ContainsDialog(session.Key), Is.False);
    }

    [Test]
    public async Task OtherCommandsEndPagingAndArePassedToOtherPlugins()
    {
        var session = new Session(21);
        await session.Start();
        Assert.That(await session.Send("mai info 22"), Is.EqualTo(MarisaPluginTaskState.Canceled));
        Assert.That(DialogManager.ContainsDialog(session.Key), Is.False);
        Assert.That(session.TextReplies(), Is.Empty);
    }

    [Test]
    public async Task RenderFailureIsPropagatedAndReleasesPaging()
    {
        var session = new Session(21) { FailPage = 2 };
        await session.Start();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send("p2"));
        Assert.That(DialogManager.ContainsDialog(session.Key), Is.False);
    }

    private sealed class Session
    {
        private readonly MessageQueueProvider _queue = new();
        private readonly MaiMaiDx.MaiMaiDx _plugin = (MaiMaiDx.MaiMaiDx)RuntimeHelpers.GetUninitializedObject(typeof(MaiMaiDx.MaiMaiDx));
        private readonly MaiVersusBatch _batch;
        private readonly long _sender = Interlocked.Increment(ref _nextSender);
        public readonly List<int> Rendered = [];
        public int? FailPage { get; init; }
        public (long?, long?) Key => (null, _sender);

        public Session(int count)
        {
            var charts = Enumerable.Range(1, count)
                .Select(id => (14.0, 3, MaiMaiVersusCommandTest.Song(id, $"song-{id}"))).ToArray();
            var player = new MaiVersusBatch.Player("player", new Dictionary<(long, int), SongScore>());
            _batch = new MaiVersusBatch("彩代", "", "定数降序", charts, player, player);
        }

        public Task Start()
        {
            Func<int, Task<string>> render = page =>
            {
                if (FailPage == page) throw new InvalidOperationException("render failed");
                Rendered.Add(page);
                return Task.FromResult("");
            };
            return (Task)typeof(MaiMaiDx.MaiMaiDx).GetMethod("ReplyPages", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(_plugin, [Message(""), _batch.PageCount, render])!;
        }

        public async Task<MarisaPluginTaskState> Send(string text)
        {
            Assert.That(DialogManager.TryGetDialog(Key, out var handler), Is.True);
            MarisaPluginTaskState result;
            try { result = await handler!(Message(text)); }
            catch
            {
                DialogManager.RemoveDialog(Key);
                throw;
            }
            if (result is MarisaPluginTaskState.Canceled or MarisaPluginTaskState.CompletedTask)
                DialogManager.RemoveDialog(Key);
            return result;
        }

        public string TextReplies()
        {
            var output = new List<string>();
            while (_queue.SendQueue.Reader.TryRead(out var reply)) output.Add(reply.MessageChain.Text);
            return string.Join("", output);
        }

        private Message Message(string text) => new(new MessageSenderProvider(_queue), new MessageDataId(1, 1), new MessageDataText(text))
        {
            Sender = new SenderInfo(_sender, "tester"), Type = MessageType.FriendMessage, Command = text.AsMemory()
        };
    }
}
