using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PDFProofer.Core.Data;
using PDFProofer.Core.Models;
using PDFProofer.Core.Services;

namespace PDFProofer.Api.Services;

public class JobProcessor : BackgroundService
{
    private readonly ILogger<JobProcessor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HotFolderSettings _settings;
    private readonly PdfProcessor _pdfProcessor;

    public JobProcessor(ILogger<JobProcessor> logger, IServiceScopeFactory scopeFactory, IOptions<HotFolderSettings> settings, PdfProcessor pdfProcessor)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _pdfProcessor = pdfProcessor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var jobs = await db.Jobs.Where(j => j.State == JobState.Active).ToListAsync(stoppingToken);
                foreach (var job in jobs)
                {
                    try
                    {
                        _logger.LogInformation("Processing job {job}", job.JobNumber);

                        var inputPath = job.SentFilePath; // ActiveShare file path
                        if (!File.Exists(inputPath))
                        {
                            _logger.LogWarning("Input file for job {job} not found: {path}", job.JobNumber, inputPath);
                            job.State = JobState.Error;
                            job.OperatorNotes = "Input file missing";
                            db.Update(job);
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }

                        var proofName = job.JobNumber + "-proof.pdf";
                        var proofPath = Path.Combine(_settings.ProofsSharePath, proofName);
                        _pdfProcessor.GenerateProofPdf(inputPath, proofPath);

                        // Optimize proof (prefer Ghostscript when enabled)
                        var optimizedProofName = job.JobNumber + "-proof-optimized.pdf";
                        var optimizedProofPath = Path.Combine(_settings.ProofsSharePath, optimizedProofName);
                        _pdfProcessor.OptimizePdf(proofPath, optimizedProofPath, _settings.UseGhostscript);

                        var thumbName = job.JobNumber + "-thumb.png";
                        var thumbPath = Path.Combine(_settings.ProofsSharePath, thumbName);
                        _pdfProcessor.GenerateThumbnail(optimizedProofPath, thumbPath);

                        // mark job finished and record proof path
                        job.State = JobState.Finished;
                        job.SentFilePath = optimizedProofPath;
                        db.Update(job);
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation("Job {job} processed, proof at {proof}", job.JobNumber, optimizedProofPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing job {job}", job.JobNumber);
                        job.State = JobState.Error;
                        job.OperatorNotes = ex.Message;
                        db.Update(job);
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JobProcessor loop failure");
            }

            await Task.Delay(_settings.JobProcessorIntervalSeconds * 1000, stoppingToken);
        }

        _logger.LogInformation("JobProcessor stopping");
    }
}
