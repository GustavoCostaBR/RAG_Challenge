using Microsoft.Extensions.DependencyInjection;
using RAG_Challenge.Application.Orchestration;
using RAG_Challenge.Domain.Contracts;

namespace RAG_Challenge.Application.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRagOrchestrator, RagOrchestrator>();
    }
}