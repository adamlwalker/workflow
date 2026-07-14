using System.ComponentModel.DataAnnotations;

namespace PDFProofer.Core.Models;

/// <summary>
/// Represents a print job in the system (FR-JM-001)
/// </summary>
public class Job
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>Job number extracted from filename (FR-PP-001)</summary>
    public string JobNumber { get; set; } = string.Empty;
    
    /// <summary>Original filename stem (without extension)</summary>
    public string FilenameStem { get; set; } = string.Empty;
    
    /// <summary>Current processing state (FR-JM-002)</summary>
    public JobState State { get; set; } = JobState.Active;
    
    /// <summary>Current location (destination name)</summary>
    public string CurrentLocation { get; set; } = string.Empty;
    
    /// <summary>Timestamp when job was created</summary>
    public DateTime ArrivalTimestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>Operator notes (FR-JM-005)</summary>
    public string OperatorNotes { get; set; } = string.Empty;
    
    /// <summary>Path where the job was sent (FR-JM-001)</summary>
    public string SentFilePath { get; set; } = string.Empty;
}