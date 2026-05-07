using Marisa.BotDriver.Plugin;
using Marisa.Plugin.Shared.Help;
using Microsoft.AspNetCore.Mvc;

namespace Marisa.StartUp.Controllers;

[ApiController]
[Route("Api/[controller]/[action]")]
public class HelpController : Controller
{
    private readonly IEnumerable<MarisaPluginBase> _plugins;

    public HelpController(IEnumerable<MarisaPluginBase> plugins)
    {
        _plugins = plugins;
    }

    [HttpGet]
    public object Get(string? name = null)
    {
        if (name != null)
        {
            var plugin = _plugins.FirstOrDefault(p =>
                p.GetType().Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (plugin == null) return Array.Empty<object>();
            var doc = HelpGenerator.GetHelp(plugin.GetType());
            return new[] { Map(doc) };
        }
        var helpDocs = HelpGenerator.GetHelp(_plugins);
        return helpDocs.Select(Map);
    }

    private static object Map(HelpDoc doc)
    {
        return new
        {
            cmd = doc.Commands,
            doc = doc.Doc,
            param = doc.ParamDesc,
            sub = doc.SubHelp.Select(Map)
        };
    }
}
