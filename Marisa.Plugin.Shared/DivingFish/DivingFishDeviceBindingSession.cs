using Marisa.Plugin.Shared.Dialog;

namespace Marisa.Plugin.Shared.DivingFish;

public sealed class DivingFishDeviceBindingSession(
    Message message,
    string game,
    Dialog.Dialog.MessageHandler handler,
    TimeProvider timeProvider,
    Func<long, Task<bool>> verifyToken)
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly (long?, long?) _key = (message.GroupInfo?.Id, message.Sender.Id);
    private readonly DateTimeOffset _deadline = timeProvider.GetUtcNow().AddMinutes(10);
    private (string Sub, DivingFishToken Token)? _pending;
    private bool _finished;

    public async Task RunAsync(DivingFishOAuth.DeviceAuthorization device)
    {
        try
        {
            await Task.WhenAll(ExpireAsync(_lifetime.Token), PollAsync(_lifetime.Token));
        }
        finally
        {
            lock (_gate) _lifetime.Dispose();
        }

        async Task ExpireAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), timeProvider, token);
                lock (_gate)
                {
                    Finish();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        async Task PollAsync(CancellationToken token)
        {
            try
            {
                var result = await DivingFishOAuth.WaitForDeviceAuthorization(device, game, token, timeProvider);
                lock (_gate)
                {
                    if (_finished) return;
                    if (!DialogManager.TryGetDialog(_key, out var current) || current != handler)
                    {
                        Finish();
                        return;
                    }

                    if (timeProvider.GetUtcNow() >= _deadline)
                    {
                        Finish();
                        return;
                    }

                    _pending = (result.Sub, result.Token);
                    message.Reply("授权已完成，如果是你本人操作的请回复收到");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                lock (_gate)
                {
                    if (Finish()) message.Reply($"DivingFish OAuth 绑定失败：{e.Message}");
                }
            }
        }
    }

    public async Task<MarisaPluginTaskState> Confirm(Message next)
    {
        (string Sub, DivingFishToken Token) entry;
        lock (_gate)
        {
            var result = _pending;
            if (!Finish()) return MarisaPluginTaskState.Canceled;

            if (!string.Equals(next.Command.Trim().ToString(), "收到", StringComparison.Ordinal) ||
                result is not { } pending || timeProvider.GetUtcNow() >= _deadline)
            {
                return MarisaPluginTaskState.Canceled;
            }

            entry = pending;
        }

        DivingFishBindingService.Commit(next.Sender.Id, entry.Sub, entry.Token, game);

        // 授权刚完成也要实测一次：票据能被成绩接口接受才算绑定成功
        try
        {
            if (await verifyToken(next.Sender.Id))
            {
                next.Reply("ok");
                return MarisaPluginTaskState.CompletedTask;
            }

            next.Reply("DivingFish OAuth 授权已完成，但令牌验证未通过，请重新绑定");
        }
        catch (Exception e)
        {
            next.Reply($"DivingFish OAuth 授权已完成，但令牌验证失败：{e.Message}");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    private bool Finish()
    {
        if (_finished) return false;
        _finished = true;
        _pending = null;
        _lifetime.Cancel();
        return DialogManager.RemoveDialog(_key, handler);
    }
}
