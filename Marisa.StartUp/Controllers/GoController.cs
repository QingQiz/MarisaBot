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

    /// <summary>
    /// 创建短链
    /// </summary>
    [HttpPost("/api/go")]
    public IActionResult Create([FromBody] CreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest("url is required");

        var code = ShortUrlStore.CreateShortUrl(request.Url,
            request.TtlMinutes.HasValue ? TimeSpan.FromMinutes(request.TtlMinutes.Value) : null);

        return Ok(new
        {
            code,
            shortUrl = ShortUrlStore.GetShortUrl(code),
            fullUrl = request.Url
        });
    }

    public record CreateRequest(string Url, int? TtlMinutes);
}
