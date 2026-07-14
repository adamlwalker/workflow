using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PDFProofer.Core.Data;
using PDFProofer.Core.Models;

namespace PDFProofer.Api.Endpoints;

[ApiController]
[Route("api/[controller]")]
public class DestinationsEndpoint : ControllerBase
{
    private readonly AppDbContext _dbContext;
    
    public DestinationsEndpoint(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    /// <summary>
    /// Get all destinations (FR-DD-002)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Destination>>> GetDestinations()
    {
        var destinations = await _dbContext.Destinations.ToListAsync();
        return Ok(destinations);
    }
    
    /// <summary>
    /// Add a new destination (FR-DD-002)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Destination>> AddDestination(Destination destination)
    {
        _dbContext.Destinations.Add(destination);
        await _dbContext.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetDestinations), destination);
    }
}