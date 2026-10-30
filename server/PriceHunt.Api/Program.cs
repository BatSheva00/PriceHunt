using Microsoft.EntityFrameworkCore;
using PriceHunt.Api.Data;
using PriceHunt.Api.Interfaces;
using PriceHunt.Api.Orchestration;
using PriceHunt.Api.Services;
using PriceHunt.Api.Suppliers;

var builder = WebApplication.CreateBuilder(args);

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
    await db.Database.EnsureCreatedAsync();
    await SearchOrchestrator.MarkOrphanedRunsAsync(db);
}

app.UseCors();
app.MapControllers();

app.Run();
