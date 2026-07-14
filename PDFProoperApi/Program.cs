using Microsoft.EntityFrameworkCore;
using PDFProofer.Core.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

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
var hotFolder = "/Users/adam/Dev/copilot-worktrees/workflow/adamlwalker-cautious-spork/HotFolder";
Directory.CreateDirectory(hotFolder);

app.Run();
