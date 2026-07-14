using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PDFProofer.Core.Data;
using PDFProofer.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace PDFProofer.Api.Endpoints;

[ApiController]
[Route("api/[controller]")]
public class FilesEndpoint : ControllerBase
{
    private readonly HotFolderSettings _settings;
    private readonly AppDbContext _db;

    public FilesEndpoint(IOptions<HotFolderSettings> settings, AppDbContext db)
    {
        _settings = settings.Value;
        _db = db;
    }

    [HttpGet]
    public IActionResult List()
    {
        var result = new Dictionary<string, object>();
        result["hotFolder"] = ListFilesSafe(_settings.HotFolderPath);
        result["activeShare"] = ListFilesSafe(_settings.ActiveSharePath);
        result["proofsShare"] = ListFilesSafe(_settings.ProofsSharePath);
        result["errorPath"] = ListFilesSafe(_settings.ErrorPath);
        return Ok(result);
    }

    private object ListFilesSafe(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return new string[0];
            var files = Directory.GetFiles(path).Select(fp => new {
                name = Path.GetFileName(fp),
                path = fp,
                length = new FileInfo(fp).Length,
                modified = System.IO.File.GetLastWriteTimeUtc(fp)
            }).OrderByDescending(f => f.modified).ToArray();
            return files;
        }
        catch
        {
            return new string[0];
        }
    }

    [HttpPost("createjob")]
    public async Task<IActionResult> CreateJob([FromForm] string folder, [FromForm] string filename)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(filename)) return BadRequest();
        string sourcePath = Path.Combine(folder, filename);
        if (!System.IO.File.Exists(sourcePath)) return NotFound("source");

        // Ensure ActiveShare exists
        Directory.CreateDirectory(_settings.ActiveSharePath);
        var destPath = Path.Combine(_settings.ActiveSharePath, filename);

        System.IO.File.Copy(sourcePath, destPath, true);

        // Create DB job
        var job = new Job
        {
            JobNumber = Path.GetFileNameWithoutExtension(filename),
            FilenameStem = Path.GetFileNameWithoutExtension(filename),
            State = JobState.Active,
            ArrivalTimestamp = DateTime.UtcNow,
            CurrentLocation = _settings.ActiveSharePath
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        return Ok(new { jobId = job.Id, path = destPath });
    }

    [HttpPost("delete")]
    public IActionResult Delete([FromForm] string folder, [FromForm] string filename)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(filename)) return BadRequest();
        var p = Path.Combine(folder, filename);
        if (!System.IO.File.Exists(p)) return NotFound();
        System.IO.File.Delete(p);
        return Ok();
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string folder, [FromQuery] string filename)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(filename)) return BadRequest();
        var p = Path.Combine(folder, filename);
        if (!System.IO.File.Exists(p)) return NotFound();
        var bytes = System.IO.File.ReadAllBytes(p);
        return File(bytes, "application/pdf", filename);
    }
}
