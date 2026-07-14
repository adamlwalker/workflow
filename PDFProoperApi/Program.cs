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

// Configure SQLite database (CON-T-001)
var connectionString = "Data Source=pdfproofer.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

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
var hotSettings = hot.Exists() ? hot.Get<PDFProofer.Core.Models.HotFolderSettings>() : new PDFProofer.Core.Models.HotFolderSettings();

// Ensure directories exist
Directory.CreateDirectory(hotSettings.HotFolderPath);
Directory.CreateDirectory(hotSettings.ActiveSharePath);
Directory.CreateDirectory(hotSettings.ProofsSharePath);
Directory.CreateDirectory(hotSettings.ErrorPath);

app.Run();
