using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/rag/ask", async (RagRequest request, IRagOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.GenerateAnswerAsync(request, ct);
    return Results.Ok(result);
});

app.Run();