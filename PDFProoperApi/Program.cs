using Microsoft.EntityFrameworkCore;
using PDFProofer.Core.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Hot folder settings
builder.Services.Configure<PDFProofer.Core.Models.HotFolderSettings>(builder.Configuration.GetSection("HotFolderSettings"));

// Register PdfProcessor from core and hosted services
builder.Services.AddSingleton<PDFProofer.Core.Services.PdfProcessor>();
builder.Services.AddHostedService<PDFProofer.Api.Services.HotFolderWatcher>();
builder.Services.AddHostedService<PDFProofer.Api.Services.JobProcessor>();

// Configure database
var connectionString = "Data Source=pdfproofer.db";
if (builder.Environment.IsDevelopment())
{
    // Use in-memory provider for local dev to avoid native sqlite dependency issues in this environment
    builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("pdfproofer_dev"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Ensure SQLite native provider initialized for runtime
try
{
    SQLitePCL.Batteries_V2.Init();
}
catch { /* best-effort init */ }

// Apply database migrations at startup (CON-O-004)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

app.UseAuthorization();

app.MapControllers();

// Start file watcher (FR-FW-002)
// Read hotfolder settings (fallback to defaults if missing)
var cfg = app.Services.GetRequiredService<IConfiguration>();
var hot = cfg.GetSection("HotFolderSettings");
var hotSettings = hot.Exists()
    ? hot.Get<PDFProofer.Core.Models.HotFolderSettings>() ?? new PDFProofer.Core.Models.HotFolderSettings()
    : new PDFProofer.Core.Models.HotFolderSettings();

// Ensure directories exist (use safe defaults if any path is null)
Directory.CreateDirectory(hotSettings.HotFolderPath ?? Path.Combine(AppContext.BaseDirectory, "HotFolder"));
Directory.CreateDirectory(hotSettings.ActiveSharePath ?? Path.Combine(AppContext.BaseDirectory, "ActiveShare"));
Directory.CreateDirectory(hotSettings.ProofsSharePath ?? Path.Combine(AppContext.BaseDirectory, "ProofsShare"));
Directory.CreateDirectory(hotSettings.ErrorPath ?? Path.Combine(AppContext.BaseDirectory, "ErrorDir"));

app.Run();
