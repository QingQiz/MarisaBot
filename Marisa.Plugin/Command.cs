using System.Diagnostics;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity;

namespace Marisa.Plugin;

[MarisaPluginNoDoc]
[MarisaPlugin(PluginPriority.Command)]
[MarisaPluginCommand(":cmd")]
public class Command : MarisaPluginBase
{
    private static IEnumerable<long> Commander => ConfigurationManager.Configuration.Commander;

    [MarisaPluginCommand(MessageType.GroupMessage, false, "filter")]
    private static MarisaPluginTaskState Filter(Message m)
    {
        m.Reply("错误的命令格式");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginSubCommand(nameof(Filter))]
    [MarisaPluginCommand(MessageType.GroupMessage, true, "remove")]
    private static MarisaPluginTaskState Remove(Message m)
    {
        if (m.Sender!.Id != 642191352)
        {
            m.Reply("你没有资格");
            return MarisaPluginTaskState.CompletedTask;
        }

        var db = new BotDbContext();

        if (!db.CommandFilters.Any())
        {
            m.Reply("无");
            return MarisaPluginTaskState.CompletedTask;
        }

        var reply = string.Join('\n', db.CommandFilters.Select(x => $"{x.Id}\t{x.Prefix}\t{x.Type}"));

        m.Reply(reply);

        Dialog.AddHandler(m.GroupInfo?.Id, m.Sender?.Id, next =>
        {
            if (next.Sender!.Id != m.Sender!.Id)
            {
                return Task.FromResult(MarisaPluginTaskState.NoResponse);
            }

            var ids = next.Command.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var id in ids)
            {
                if (long.TryParse(next.Command, out var idLong))
                {
                    db.CommandFilters.Remove(db.CommandFilters.First(x => x.Id == idLong));
                }
            }

            db.SaveChanges();

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginSubCommand(nameof(Filter))]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "prefix")]
    private static MarisaPluginTaskState Prefix(Message m)
    {
        if (m.Sender!.Id != 642191352)
        {
            m.Reply("你没有资格");
            return MarisaPluginTaskState.CompletedTask;
        }

        var prefix = m.Command.Trim();

        if (string.IsNullOrWhiteSpace(prefix))
        {
            m.Reply("?");
            return MarisaPluginTaskState.CompletedTask;
        }

        var db = new BotDbContext();
        db.CommandFilters.Add(new CommandFilter
        {
            GroupId = m.GroupInfo!.Id,
            Prefix  = prefix,
            Type    = "",
        });
        db.SaveChanges();

        m.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginSubCommand(nameof(Filter))]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "type")]
    private static MarisaPluginTaskState Type(Message m)
    {
        if (m.Sender!.Id != 642191352)
        {
            m.Reply("你没有资格");
            return MarisaPluginTaskState.CompletedTask;
        }

        var names = Enum.GetNames(typeof(MessageDataType));

        var type = m.Command.Trim();

        if (string.IsNullOrWhiteSpace(type))
        {
            m.Reply(string.Join('\n', names));
            return MarisaPluginTaskState.CompletedTask;
        }

        if (!names.Contains(type))
        {
            m.Reply($"错误的类型：`{type}`");
            return MarisaPluginTaskState.CompletedTask;
        }

        var db = new BotDbContext();
        db.CommandFilters.Add(new CommandFilter
        {
            GroupId = m.GroupInfo!.Id,
            Prefix  = "",
            Type    = type,
        });
        db.SaveChanges();

        m.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// start an interactive shell
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("shell")]
    private static MarisaPluginTaskState Shell(Message m)
    {
        if (!Commander.Contains(m.Sender!.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
        }
        else
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe")
                {
                    Arguments              = "/Q /K @echo off & cd c:\\",
                    UseShellExecute        = false,
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                },
            };
            var output = "";

            proc.OutputDataReceived += (_, args) =>
            {
                if (args.Data == "CC_DONE")
                {
                    m.Reply(output.Trim(), false);
                }

                output += args.Data?.Trim() + "\n";
            };
            proc.ErrorDataReceived += (_, args) => { output += args.Data?.Trim() + "\n"; };
            proc.Start();
            proc.StandardInput.WriteLine("@echo off");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            Dialog.AddHandler(m.GroupInfo?.Id, m.Sender.Id, message =>
            {
                var command = message.Command;

                if (command is "exit")
                {
                    proc.Close();
                    message.Reply("Shell退出了");
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                }

                output = "";
                proc.StandardInput.WriteLine(command);
                proc.StandardInput.WriteLine("echo CC_DONE");

                return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
            });

            m.Reply("Shell启动了");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// restart bot
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("reboot", "restart")]
    private static MarisaPluginTaskState Reboot(Message m)
    {
        if (!Commander.Contains(m.Sender!.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
        }
        else
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName        = Environment.GetCommandLineArgs()[0].Replace(".dll", ".exe"),
                    Arguments       = string.Join(' ', Environment.GetCommandLineArgs().Skip(1)),
                    UseShellExecute = true,
                    CreateNoWindow  = true,
                }
            };
            proc.Start();

            Environment.Exit(0);
        }

        return MarisaPluginTaskState.CompletedTask;
    }
}