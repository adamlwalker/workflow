namespace PDFProofer.Core.Models;

public class HotFolderSettings
{
    public string HotFolderPath { get; set; } = "./HotFolder";
    public string ActiveSharePath { get; set; } = "./ActiveShare";
    public string ProofsSharePath { get; set; } = "./ProofsShare";
    public string ErrorPath { get; set; } = "./ErrorDir";
    public int JobProcessorIntervalSeconds { get; set; } = 5;
    // When true, the JobProcessor will attempt to use Ghostscript for PDF optimization
    public bool UseGhostscript { get; set; } = false;
}