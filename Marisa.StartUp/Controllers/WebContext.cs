using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebContextStore = Marisa.Plugin.Shared.Util.WebContext;

namespace Marisa.StartUp.Controllers;

[ApiController]
[Route("Api/[controller]/[action]")]
public class WebContext : Controller
{
    public WebContext()
    {
        _ = WebContextStore.EnsureHistoryPath();
    }

    private static bool TryReadHistory(Guid id, string name, out string output)
    {
        var path = WebContextStore.EnsureHistoryPath();

        if (id == Guid.Empty)
        {
            foreach (var i in Directory.GetFiles(path))
            {
                var file = Path.GetFileName(i);
                if (file.StartsWith(name))
                {
                    output = System.IO.File.ReadAllText(i);
                    return true;
                }
            }
        }
        else
        {
            foreach (var i in Directory.GetFiles(path))
            {
                var file = Path.GetFileName(i);
                if (file.StartsWith(name) && file.EndsWith(id.ToString()))
                {
                    output = System.IO.File.ReadAllText(i);
                    return true;
                }
            }
        }

        output = "";
        return false;
    }

    [HttpGet]
    public string Get(Guid id, string name)
    {
        if (TryReadHistory(id, name, out var output))
        {
            return output;
        }

        var obj = WebContextStore.Get(id, name);
        var str = obj is string ? obj.ToString()! : JsonConvert.SerializeObject(obj);

        Task.Run(() => WebContextStore.Dump(id, name, str));

        return str;
    }
}
