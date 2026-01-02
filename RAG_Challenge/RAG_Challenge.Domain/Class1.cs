namespace RAG_Challenge.Domain;

public record ExternalApiAResponse(string Id, string Name, string Status);
public record ExternalApiBResponse(string Code, string Description);

public interface IExternalApiAClient
{
    Task<ExternalApiAResponse?> GetItemAsync(string id, CancellationToken cancellationToken = default);
}

public interface IExternalApiBClient
{
    Task<IReadOnlyList<ExternalApiBResponse>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public interface IWeatherService
{
    Task<IReadOnlyList<WeatherForecast>> GetForecastAsync(CancellationToken cancellationToken = default);
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
