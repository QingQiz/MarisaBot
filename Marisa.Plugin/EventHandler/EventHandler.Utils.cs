using System.Diagnostics;
using NLog;

namespace Marisa.Plugin.EventHandler;

public partial class EventHandler
{
    /// <summary>
    /// 戳一戳
    /// </summary>
    private static void NudgeHandler(Message message, MessageData msg, long qq)
    {
        var m = (msg as MessageDataNudge)!;

        if (m.Target != qq) return;

        const string word = "别戳啦！";

        if (message.GroupInfo != null)
        {
            message.Reply(
                new MessageDataAt(m.FromId),
                new MessageDataText(" "),
                new MessageDataText(word)
            );
        }
        else
        {
            message.Reply(word);
        }
    }

    /// <summary>
    /// 新人入群
    /// </summary>
    private static void NewMemberHandler(Message message, MessageData msg, long qq)
    {
        var m = (msg as MessageDataNewMember)!;
        // 被人邀请
        if (m.InvitorId != null && m.InvitorId != 0)
        {
            message.Reply(new MessageDataAt((long)m.InvitorId),
                new MessageDataText("邀请"),
                new MessageDataAt(m.Id),
                new MessageDataText("加入本群！欢迎！"));
        }
        else
        {
            message.Reply(new MessageDataAt(m.Id), new MessageDataText("加入本群！欢迎！"));
        }
    }

    /// <summary>
    /// 退群 / 被踢
    /// </summary>
    private static void MemberLeaveHandler(Message message, MessageData msg, long qq)
    {
        var m = (msg as MessageDataMemberLeave)!;

        if (m.Kicker == null)
        {
            message.Reply($"{m.Name} ({m.Id}) 退群了");
        }
        else
        {
            message.Reply(new MessageDataText($"{m.Name} ({m.Id}) 被"),
                new MessageDataAt((long)m.Kicker),
                new MessageDataText("踢了")
            );
        }
    }

    private static void BotMuteHandler(Message message, MessageData msg, long qq)
    {
        var m = (msg as MessageDataBotMute)!;

        var now = DateTime.Now;
        var log = LogManager.GetCurrentClassLogger();

        Dialog.TryAddHandler(m.GroupId, null, message1 =>
        {
            // 超过了禁言时间
            if (DateTime.Now - now > m.Time)
            {
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            // 收到了解除禁言的消息
            if ((message1.MessageChain?.Messages.Any(md => md.Type == MessageDataType.BotUnmute) ?? false)
             && (message1.GroupInfo?.Id ?? 0) == m.GroupId)
            {
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            log.Warn("少女祈祷中...");

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        });
    }

    private class Debounce(int delayL, int delayR)
    {
        private Timer? _timer;
        private readonly object _lock = new();
        private bool _isThrottled;

        public void Execute(Action action)
        {
            lock (_lock)
            {
                if (_isThrottled) return;

                _timer?.Dispose();
                _timer = new Timer(_ =>
                {
                    action();
                    lock (_lock)
                    {
                        _isThrottled = true;
                        _timer = new Timer(_ =>
                        {
                            lock (_lock)
                            {
                                _isThrottled = false;
                            }
                        }, null, delayR, Timeout.Infinite);
                    }
                }, null, delayL, Timeout.Infinite);
            }
        }

        public void Cancel()
        {
            lock (_lock)
            {
                if (_isThrottled) return;

                _timer?.Dispose();
                _isThrottled = false;
            }
        }
    }

    private static void KillSignServer()
    {
        // exit if not linux
        if (Environment.OSVersion.Platform != PlatformID.Unix) return;

        // kill sign server

        // get all java processes and filter by its command line
        const string command   = "/bin/bash";
        const string arguments = "-c \"ps aux | grep '[j]ava' \"";

        var procStartInfo = new ProcessStartInfo(command, arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var proc = new Process();

        proc.StartInfo = procStartInfo;
        proc.Start();
        using var reader = proc.StandardOutput;

        var result = reader.ReadToEnd()
            .Split(Environment.NewLine)
            .First(x => x.Contains("unidbg-fetch-qsign"))
            .Split(' ', 11, StringSplitOptions.RemoveEmptyEntries);

        var pid = result[1];
        var cmd = result.Last();

        Logger.Info("Killing sign server: {0}...", cmd[..20]);

        KillProcess(pid);
    }

    private static void KillProcess(string pid)
    {
        try
        {
            // Start the bash shell
            using var proc = new Process();
            proc.StartInfo.FileName        = "/bin/bash";
            proc.StartInfo.Arguments       = $"-c \"kill -9 {pid}\"";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow  = true;

            proc.Start();
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                Logger.Info($"Process {pid} has been killed successfully.");
            }
            else
            {
                Logger.Error($"Failed to kill process {pid}. Exit code: {proc.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"An error occurred on killing process {pid}: {ex.Message}");
        }
    }
}