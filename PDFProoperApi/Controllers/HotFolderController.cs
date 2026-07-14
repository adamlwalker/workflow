using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PDFProofer.Core.Models;
using System.Text.Json;

namespace PDFProofer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HotFolderController : ControllerBase
{
    private readonly HotFolderSettings _settings;
    private readonly IConfiguration _configuration;

    public HotFolderController(IOptions<HotFolderSettings> settings, IConfiguration configuration)
    {
        _settings = settings.Value;
        _configuration = configuration;
    }

    [HttpGet]
    public ActionResult Get()
    {
        var detected = PDFProofer.Core.Services.GhostscriptHelper.IsAvailable();
        return Ok(new { Settings = _settings, GhostscriptDetected = detected });
    }

    [HttpPost]
    public ActionResult Set([FromBody] HotFolderSettings incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming.HotFolderPath)) return BadRequest("HotFolderPath required");

        // Persist new settings to appsettings.json so restart or other processes can pick them up.
        // Note: this is a simple file write for the development scaffold. A production app should
        // store settings in a database or user secrets / config provider.
        try
        {
            var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (!System.IO.File.Exists(appSettingsPath)) return StatusCode(500, "appsettings.json not found");

            var json = System.IO.File.ReadAllText(appSettingsPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.NameEquals("HotFolderSettings"))
                    {
                        // write updated HotFolderSettings
                        writer.WritePropertyName("HotFolderSettings");
                        JsonSerializer.Serialize(writer, incoming);
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            System.IO.File.WriteAllBytes(appSettingsPath, stream.ToArray());
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }

        return Ok(incoming);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded");

        var destDir = _settings.HotFolderPath;
        Directory.CreateDirectory(destDir);
        var destPath = Path.Combine(destDir, file.FileName);

        await using var fs = System.IO.File.Create(destPath);
        await file.CopyToAsync(fs);

        return Ok(new { saved = destPath });
    }
}
