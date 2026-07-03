using Marisa.Plugin.Shared.Lxns;
using Microsoft.AspNetCore.Mvc;

namespace Marisa.StartUp.Controllers;

[ApiController]
public class GoController : Controller
{
    /// <summary>
    /// 短链重定向 (302)
    /// </summary>
    [HttpGet("/go/{code}")]
    public IActionResult Index(string code)
    {
        var url = ShortUrlStore.GetUrl(code);
        if (url == null) return NotFound("链接已过期或不存在");
        return Redirect(url);
    }
}
