using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Infrastructure.Clients;
using RAG_Challenge.Infrastructure.Configuration;
using RAG_Challenge.Infrastructure.Orchestration;

namespace RAG_Challenge.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<VectorDbOptions>(configuration.GetSection(VectorDbOptions.SectionName));

        services.AddHttpClient<IOpenAiClient, OpenAiHttpClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddHttpClient<IVectorDbClient, VectorDbHttpClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<VectorDbOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddScoped<IRagOrchestrator, RagOrchestrator>();
    }
}