using RAG_Challenge.Application;
using RAG_Challenge.Domain;
using RAG_Challenge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IWeatherService, WeatherForecastService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/weather", async (IWeatherService service, CancellationToken ct) =>
{
    var forecast = await service.GetForecastAsync(ct);
    return Results.Ok(forecast);
})
.WithName("GetWeather")
.WithOpenApi();

app.MapGet("/external/a/{id}", async (string id, IExternalApiAClient client, CancellationToken ct) =>
{
    var item = await client.GetItemAsync(id, ct);
    return item is null ? Results.NotFound() : Results.Ok(item);
}).WithName("GetExternalA").WithOpenApi();

app.MapGet("/external/b", async (string q, IExternalApiBClient client, CancellationToken ct) =>
{
    var items = await client.SearchAsync(q, ct);
    return Results.Ok(items);
}).WithName("SearchExternalB").WithOpenApi();

app.Run();
