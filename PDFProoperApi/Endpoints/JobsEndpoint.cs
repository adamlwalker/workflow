using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PDFProofer.Core.Data;
using PDFProofer.Core.Models;

namespace PDFProofer.Api.Endpoints;

[ApiController]
[Route("api/[controller]")]
public class JobsEndpoint : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public JobsEndpoint(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Job>>> GetJobs()
    {
        var jobs = await _dbContext.Jobs.OrderByDescending(j => j.ArrivalTimestamp).ToListAsync();
        return Ok(jobs);
    }

    [HttpPost("{id}/reprocess")]
    public async Task<IActionResult> Reprocess(int id)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null) return NotFound();

        job.State = JobState.Active;
        job.OperatorNotes = "Manually reprocessed";
        _dbContext.Update(job);
        await _dbContext.SaveChangesAsync();

        return Ok(job);
    }

    [HttpPost("{id}/send/{destinationId}")]
    public async Task<IActionResult> SendToDestination(int id, int destinationId)
    {
        var job = await _dbContext.Jobs.FindAsync(id);
        if (job == null) return NotFound();

        var dest = await _dbContext.Destinations.FindAsync(destinationId);
        if (dest == null) return NotFound("destination");

        try
        {
            Directory.CreateDirectory(dest.TargetFolderPath);
            var source = job.SentFilePath;
            if (!System.IO.File.Exists(source)) return NotFound("source file");

            var targetFile = Path.Combine(dest.TargetFolderPath, Path.GetFileName(source));
            System.IO.File.Copy(source, targetFile, true);

            job.State = JobState.Sent;
            job.CurrentLocation = dest.DisplayName;
            _dbContext.Update(job);
            await _dbContext.SaveChangesAsync();

            return Ok(new { sentTo = targetFile });
        }
        catch (Exception ex)
        {
            job.State = JobState.Error;
            job.OperatorNotes = ex.Message;
            _dbContext.Update(job);
            await _dbContext.SaveChangesAsync();
            return StatusCode(500, ex.Message);
        }
    }
}
