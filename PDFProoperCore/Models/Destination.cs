using System.ComponentModel.DataAnnotations;

namespace PDFProofer.Core.Models;

/// <summary>
/// Represents a production destination (FR-DD-001)
/// </summary>
public class Destination
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>Unique destination identifier</summary>
    public string DestinationId { get; set; } = string.Empty;
    
    /// <summary>Display name for the destination</summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>Target folder path (local or UNC share)</summary>
    public string TargetFolderPath { get; set; } = string.Empty;
    
    /// <summary>Whether to copy optimized version (vs original)</summary>
    public bool UseOptimizedVersion { get; set; } = true;
    
    /// <summary>Optional display color for UI</summary>
    public string DisplayColor { get; set; } = "#007bff";
}