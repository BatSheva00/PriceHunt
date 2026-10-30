using Microsoft.EntityFrameworkCore;
using PriceHunt.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=pricehunt.db";

builder.Services.AddDbContext<PriceHuntDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PriceHuntDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
