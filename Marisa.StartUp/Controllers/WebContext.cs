using Microsoft.AspNetCore.Mvc;

namespace Marisa.StartUp;

[ApiController]
[Route("Api/[controller]/[action]")]
public class WebContext : Controller
{
    [HttpGet]
    public object Get(Guid id, string name) {
        return Utils.WebContext.Get(id, name);
    }
}
