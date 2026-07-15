using Marisa.Plugin.Shared.Lxns;
using Microsoft.AspNetCore.Mvc;

namespace Marisa.StartUp.Controllers;

[ApiController]
[Route("Api/[controller]/[action]")]
public class MaiMai : Controller
{
    /// <summary>
    /// 歌曲的 simai 谱面文本，供谱面预览页使用
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Chart(long id)
    {
        var chart = await LxnsChartStore.GetChart(id);
        if (chart == null) return NotFound("谱面不存在");

        Response.Headers.CacheControl = "public, max-age=86400";
        return Content(chart, "text/plain; charset=utf-8");
    }
}
