namespace PDFProofer.Core.Models;

/// <summary>
/// Job processing states as defined in FR-JM-002
/// </summary>
public enum JobState
{
    /// <summary>Processed successfully; visible in operator queue</summary>
    Active,
    
    /// <summary>Processing failed; visible in operator queue with error indicator</summary>
    Error,
    
    /// <summary>Dispatched to a destination; auto-hides when destination file is consumed</summary>
    Sent,
    
    /// <summary>Manually marked complete or hidden by operator</summary>
    Finished
}