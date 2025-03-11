using System.Diagnostics;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity;
using Marisa.Plugin.Shared.Interface;

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
        if (m.Sender.Id != 642191352)
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

        Dialog.TryAddHandler(m.GroupInfo?.Id, m.Sender?.Id, next =>
        {
            if (next.Sender.Id != m.Sender!.Id)
            {
                return Task.FromResult(MarisaPluginTaskState.NoResponse);
            }

            var ids = next.Command.Split(',').Where(x => x.Length != 0);

            foreach (var id in ids)
            {
                if (long.TryParse(next.Command.Span, out var idLong))
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
        if (m.Sender.Id != 642191352)
        {
            m.Reply("你没有资格");
            return MarisaPluginTaskState.CompletedTask;
        }

        var prefix = m.Command.Trim();

        if (prefix.IsWhiteSpace())
        {
            m.Reply("?");
            return MarisaPluginTaskState.CompletedTask;
        }

        var db = new BotDbContext();
        db.CommandFilters.Add(new CommandFilter
        {
            GroupId = m.GroupInfo!.Id,
            Prefix  = prefix.ToString(),
            Type    = ""
        });
        db.SaveChanges();

        m.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginSubCommand(nameof(Filter))]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "type")]
    private static MarisaPluginTaskState Type(Message m)
    {
        if (m.Sender.Id != 642191352)
        {
            m.Reply("你没有资格");
            return MarisaPluginTaskState.CompletedTask;
        }

        var names = Enum.GetNames(typeof(MessageDataType));

        var type = m.Command.Trim();

        if (type.IsWhiteSpace())
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
            Type    = type.ToString()
        });
        db.SaveChanges();

        m.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     start an interactive shell
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("shell")]
    private static MarisaPluginTaskState Shell(Message m)
    {
        if (!Commander.Contains(m.Sender.Id))
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
                    RedirectStandardError  = true
                }
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

            Dialog.TryAddHandler(m.GroupInfo?.Id, m.Sender.Id, message =>
            {
                var command = message.Command;

                if (command.Span is "exit")
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
    ///     restart bot
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("reboot", "restart")]
    private static MarisaPluginTaskState Reboot(Message m)
    {
        if (!Commander.Contains(m.Sender.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        var currentProcess = Process.GetCurrentProcess();

        var fileName  = currentProcess.MainModule!.FileName;
        var arguments = Environment.GetCommandLineArgs();

        var startInfo = new ProcessStartInfo
        {
            FileName        = fileName,
            Arguments       = string.Join(' ', arguments.Skip(1)),
            UseShellExecute = false
        };

        Process.Start(startInfo);

        Environment.Exit(0);

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     reset bot cache
    /// </summary>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("reset")]
    private static MarisaPluginTaskState Reset(Message m, IEnumerable<MarisaPluginBase> plugins)
    {
        if (!Commander.Contains(m.Sender.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        var filtered =
            from plugin in plugins
            where plugin.GetType().IsSubclassOf(typeof(ICanReset))
            select plugin as ICanReset;

        foreach (var canReset in filtered)
        {
            canReset.Reset();
        }

        m.Reply("Done.");
        return MarisaPluginTaskState.CompletedTask;
    }
}