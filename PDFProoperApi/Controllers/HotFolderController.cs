using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PDFProofer.Core.Models;

namespace PDFProofer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HotFolderController : ControllerBase
{
    private readonly HotFolderSettings _settings;

    public HotFolderController(IOptions<HotFolderSettings> settings)
    {
        _settings = settings.Value;
    }

    [HttpGet]
    public ActionResult<HotFolderSettings> Get()
    {
        return Ok(_settings);
    }

    [HttpPost]
    public ActionResult Set([FromBody] HotFolderSettings incoming)
    {
        // Settings are persisted via appsettings.json in this scaffold; real persistence requires a config store.
        // For now, validate and echo back settings.
        if (string.IsNullOrWhiteSpace(incoming.HotFolderPath)) return BadRequest("HotFolderPath required");
        return Ok(incoming);
    }
}
