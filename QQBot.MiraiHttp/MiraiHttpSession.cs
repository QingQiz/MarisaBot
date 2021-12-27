using Flurl.Http;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.MiraiHttp;

public partial class MiraiHttpSession
{
    private readonly string _serverAddress;
    private readonly string _authKey;
    private readonly IEnumerable<MiraiPluginBase> _plugins;

    private readonly MessageQueueProvider _messageQueue;

    private readonly IServiceProvider _serviceProvider;
    private readonly DictionaryProvider _dictionaryProvider;

    public MiraiHttpSession(DictionaryProvider dict, IServiceProvider provider, IEnumerable<MiraiPluginBase> plugins, MessageQueueProvider messageQueue)
    {
        _serverAddress      = dict["ServerAddress"];
        Id                  = dict["QQ"];
        _authKey            = dict["AuthKey"];
        _plugins            = plugins;
        _messageQueue       = messageQueue;
        _serviceProvider    = provider;
        _dictionaryProvider = dict;

        foreach (var plugin in _plugins)
        {
            AddPlugin(plugin);
        }
    }

    private string _session = null!;

    private delegate Task EventHandler(MiraiHttpSession session, dynamic message);

    private event EventHandler OnEvent = null!;

    private static void CheckResponse(dynamic response)
    {
        if (response.code != 0) throw new Exception($"[Code {response.code}] {response.msg}");
    }

    public long Id { get; }

    private void AddPlugin(MiraiPluginBase miraiPlugin)
    {
        OnEvent += miraiPlugin.EventHandler;
    }

    private async Task Auth()
    {
        // get session
        var login = await (await $"{_serverAddress}/verify".PostJsonAsync(new { verifyKey = _authKey })).GetJsonAsync();
        CheckResponse(login);

        _session = login.session;

        CheckResponse(await
            (await $"{_serverAddress}/bind".PostJsonAsync(new { sessionKey = _session, qq = Id })).GetJsonAsync());
    }

    public async Task Invoke()
    {
        var recv = RecvMessage();
        var send = SendMessage();
        await ProcMessage();

        await recv;
        await send;
    }
}