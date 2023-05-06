using Marisa.Plugin.Shared.MaiMaiDx;
using Microsoft.AspNetCore.Mvc;

namespace Marisa.StartUp.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class MaiMai : Controller
{
    [HttpGet("RaNew")]
    public IEnumerable<int> RaNew([FromQuery]decimal[] constants, [FromQuery]decimal[] achievements)
    {
        return constants.Zip(achievements, (c, a) => SongScore.B50Ra(a, c));
    }
}