using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG_Challenge.Domain;

namespace RAG_Challenge.Infrastructure;

public sealed class ExternalApiAOptions
{
    public const string SectionName = "ExternalApis:OpenAI";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

public sealed class ExternalApiBOptions
{
    public const string SectionName = "ExternalApis:VectorDb";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

internal sealed class ExternalApiAClient(HttpClient httpClient, IOptions<ExternalApiAOptions> options, ILogger<ExternalApiAClient> logger) : IExternalApiAClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ExternalApiAOptions _options = options.Value;
    private readonly ILogger<ExternalApiAClient> _logger = logger;

    public async Task<ExternalApiAResponse?> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"items/{id}");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("X-API-KEY", _options.ApiKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("ApiA returned non-success status {StatusCode} for id {Id}", response.StatusCode, id);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ExternalApiAResponse>(cancellationToken: cancellationToken);
    }
}

internal sealed class ExternalApiBClient(HttpClient httpClient, IOptions<ExternalApiBOptions> options, ILogger<ExternalApiBClient> logger) : IExternalApiBClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ExternalApiBOptions _options = options.Value;
    private readonly ILogger<ExternalApiBClient> _logger = logger;

    public async Task<IReadOnlyList<ExternalApiBResponse>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"search?q={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("X-API-KEY", _options.ApiKey);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("ApiB returned non-success status {StatusCode} for query {Query}", response.StatusCode, query);
            return Array.Empty<ExternalApiBResponse>();
        }

        var items = await response.Content.ReadFromJsonAsync<List<ExternalApiBResponse>>(cancellationToken: cancellationToken);
        return items ?? [];
    }
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExternalApiAOptions>(configuration.GetSection(ExternalApiAOptions.SectionName));
        services.Configure<ExternalApiBOptions>(configuration.GetSection(ExternalApiBOptions.SectionName));

        services.AddHttpClient<IExternalApiAClient, ExternalApiAClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ExternalApiAOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddHttpClient<IExternalApiBClient, ExternalApiBClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ExternalApiBOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        return services;
    }
}
