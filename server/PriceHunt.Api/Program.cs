using Microsoft.EntityFrameworkCore;
using PriceHunt.Api.Data;
using PriceHunt.Api.Interfaces;
using PriceHunt.Api.Orchestration;
using PriceHunt.Api.Services;
using PriceHunt.Api.Suppliers;

var builder = WebApplication.CreateBuilder(args);

// Render injects PORT. Local `dotnet run` does not, and keeps the launch profile URL.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=pricehunt.db";

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<PriceHuntDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddHttpClient("places", client =>
{
    client.BaseAddress = new Uri("https://data.gov.il/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PriceHunt/1.0");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<IPlaceService, PlaceService>();
builder.Services.AddSingleton<SupplierCatalog>();
builder.Services.AddSingleton<SearchStreamGate>();
builder.Services.AddScoped<SearchOrchestrator>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PriceHuntDbContext>();
    await db.Database.MigrateAsync();
    await SearchOrchestrator.MarkOrphanedRunsAsync(db);
}

app.UseCors();

var indexFile = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
var serveClient = File.Exists(indexFile);
if (serveClient)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapControllers();

if (serveClient)
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexFile);
    });
}

app.Run();

public partial class Program
{
}
