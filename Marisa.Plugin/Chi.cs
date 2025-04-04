﻿using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin;

[MarisaPluginDoc("这是一个用来解决「中午吃什么」这一人生 N 大难题之一的功能")]
[MarisaPluginTrigger(nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
public class Chi : MarisaPluginBase
{
    private const int Times = 5;
    private readonly Dictionary<long, (DateTime, int)> _cache = new();

    private bool Zuo(long id)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(id, out var value))
            {
                var (time, cnt) = value;
                if (DateTime.Now - time < TimeSpan.FromMinutes(5))
                {
                    if (cnt >= Times) return true;

                    _cache[id] = (DateTime.Now, cnt + 1);
                    return false;
                }

                _cache[id] = (DateTime.Now, 1);
                return false;
            }

            _cache[id] = (DateTime.Now, 1);
            return false;
        }
    }

    private readonly Dictionary<string, HashSet<string>> _data = new()
    {
        ["西工大"] = ConfigurationManager.Configuration.Chi.ToHashSet()
    };

    public Chi(BotDbContext dbContext)
    {
        foreach (var i in dbContext.Meals)
        {
            AddMeal(i.Place, i.Name);
        }
    }

    private void AddMeal(string place, string meal)
    {
        if (_data.TryGetValue(place, out var value))
        {
            value.Add(meal);
        }
        else
        {
            _data[place] = [meal];
        }
    }

    private string ChiSha(long id, string place)
    {
        if (Zuo(id))
        {
            return "生吃你妈 问这么多还不知道吃啥饿死你个臭傻逼";
        }

        lock (_data)
        {
            if (!string.IsNullOrWhiteSpace(place) && _data.TryGetValue(place, out var value))
            {
                return value.RandomTake(1).First();
            }
            return _data.Values.SelectMany(x => x).RandomTake(1).First();
        }
    }

    [MarisaPluginTrigger(typeof(Chi), nameof(Trigger))]
    private MarisaPluginTaskState Proc(Message message)
    {
        var sender = message.Sender.Id;
        var cmd    = message.Command;

        var place = cmd.EndsWith("啥") ? cmd[..^2] : cmd[..^3];

        message.Reply(ChiSha(sender, place.ToString()));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("现有可用的地点")]
    [MarisaPluginCommand(true, "mealplace")]
    private MarisaPluginTaskState Place(Message message, BotDbContext dbContext)
    {
        lock (_data)
        {
            var places = _data.Keys.ToList();
            var reply  = "现有可用的地点：\n" + string.Join("\n", places);
            message.Reply(reply);
        }

        return MarisaPluginTaskState.CompletedTask;
    }


    [MarisaPluginDoc("添加吃啥的可选项。参数：地点")]
    [MarisaPluginCommand("addmeal")]
    private MarisaPluginTaskState Add(Message message, BotDbContext dbContext)
    {
        var place = message.Command.ToString();

        message.Reply("吃什么？");

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var meal = next.Command.ToString();
            lock (_data)
            {
                AddMeal(place, meal);
            }

            Task.Run(() =>
            {
                dbContext.Meals.Add(new Meal(place, meal));
                dbContext.SaveChanges();
            });

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    public static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        if (!message.IsPlainText()) return false;

        return message.Command.EndsWith("吃什么", StringComparison.OrdinalIgnoreCase) ||
               message.Command.EndsWith("吃啥", StringComparison.OrdinalIgnoreCase);
    };
}