using RAG_Challenge.Application;
using RAG_Challenge.Domain;
using RAG_Challenge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IWeatherService, WeatherForecastService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/weather", async (IWeatherService service, CancellationToken ct) =>
    {
        var forecast = await service.GetForecastAsync(ct);
        return Results.Ok(forecast);
    })
    .WithName("GetWeather");

app.MapGet("/external/a/{id}", async (string id, IOpenAiClient client, CancellationToken ct) =>
{
    var item = await client.GetItemAsync(id, ct);
    return item is null ? Results.NotFound() : Results.Ok(item);
}).WithName("GetOpenAiItem");

app.MapPost("/external/b/search", async (VectorSearchRequest request, IVectorDbClient client, CancellationToken ct) =>
{
    var items = await client.SearchAsync(request, ct);
    return Results.Ok(items);
}).WithName("SearchVectorDb");

app.MapPost("/external/a/embeddings", async (EmbeddingRequest request, IOpenAiClient client, CancellationToken ct) =>
{
    var embedding = await client.CreateEmbeddingAsync(request.Input, ct);
    return embedding is null ? Results.Problem("Embedding request failed", statusCode: 502) : Results.Ok(embedding);
}).WithName("CreateEmbedding");

app.MapPost("/rag/ask", async (string question, IRagOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.GenerateAnswerAsync(question, ct);
    return Results.Ok(result);
}).WithName("RagAsk");

app.Run();