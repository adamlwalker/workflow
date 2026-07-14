using Microsoft.AspNetCore.Mvc;

namespace PDFProofer.Api.Endpoints;

[ApiController]
[Route("api/[controller]")]
public class FileEndpoint : ControllerBase
{
    private readonly string _activeSharePath = "/Users/adam/Dev/copilot-worktrees/workflow/adamlwalker-cautious-spork/ActiveShare";
    
    /// <summary>
    /// Serve file from ActiveShare (FR-UI-010)
    /// </summary>
    [HttpGet("{jobNumber}/{fileType}")]
    public IActionResult GetFile(string jobNumber, string fileType)
    {
        var filePath = Path.Combine(_activeSharePath, jobNumber, $"{fileType}.pdf");
        
        if (!System.IO.File.Exists(filePath))
            return NotFound();
        
        var fileStream = System.IO.File.OpenRead(filePath);
        return File(fileStream, "application/pdf", $"{jobNumber}_{fileType}.pdf");
    }
}