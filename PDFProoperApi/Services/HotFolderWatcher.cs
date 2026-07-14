using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PDFProofer.Core.Data;
using PDFProofer.Core.Models;
using PDFProofer.Core.Services;

namespace PDFProofer.Api.Services;

public class HotFolderWatcher : BackgroundService
{
    private readonly ILogger<HotFolderWatcher> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HotFolderSettings _settings;
    private FileSystemWatcher? _watcher;

    public HotFolderWatcher(ILogger<HotFolderWatcher> logger, IServiceScopeFactory scopeFactory, IOptions<HotFolderSettings> settings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_settings.HotFolderPath);
        Directory.CreateDirectory(_settings.ActiveSharePath);
        Directory.CreateDirectory(_settings.ProofsSharePath);
        Directory.CreateDirectory(_settings.ErrorPath);

        _watcher = new FileSystemWatcher(_settings.HotFolderPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            Filter = "*.pdf",
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnCreated;

        _logger.LogInformation("HotFolderWatcher started watching {path}", _settings.HotFolderPath);

        return Task.CompletedTask;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait for file to be fully written
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        using (var stream = File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                        break;
                    }
                    catch
                    {
                        await Task.Delay(200);
                    }
                }

                var filename = Path.GetFileName(e.FullPath) ?? Guid.NewGuid().ToString();
                var filenameStem = Path.GetFileNameWithoutExtension(filename);
                var jobNumber = filenameStem; // simple extraction; real logic can parse patterns

                var destName = jobNumber + Path.GetExtension(filename);
                var destPath = Path.Combine(_settings.ActiveSharePath, destName);

                File.Move(e.FullPath, destPath);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var job = new PDFProofer.Core.Models.Job
                {
                    JobNumber = jobNumber,
                    FilenameStem = filenameStem,
                    State = JobState.Active,
                    ArrivalTimestamp = DateTime.UtcNow,
                    OperatorNotes = "Dropped from hotfolder",
                    SentFilePath = destPath
                };

                db.Jobs.Add(job);
                await db.SaveChangesAsync();

                _logger.LogInformation("Enqueued job {job} from hotfolder", job.JobNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling hotfolder file {file}", e.FullPath);
            }
        });
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.Created -= OnCreated;
            _watcher.Dispose();
            _watcher = null;
        }

        _logger.LogInformation("HotFolderWatcher stopped");
        return base.StopAsync(cancellationToken);
    }
}
