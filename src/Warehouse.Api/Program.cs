using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Data;
using Warehouse.Api.Infrastructure;
using Warehouse.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);
var databasePath = Path.Combine(dataDirectory, "warehouse.db");

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
