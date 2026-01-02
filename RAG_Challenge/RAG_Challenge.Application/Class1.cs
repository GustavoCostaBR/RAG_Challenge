using RAG_Challenge.Domain;

namespace RAG_Challenge.Application;

public class WeatherForecastService : IWeatherService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public Task<IReadOnlyList<WeatherForecast>> GetForecastAsync(CancellationToken cancellationToken = default)
    {
        var forecast = Enumerable.Range(1, 5)
            .Select(index => new WeatherForecast(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]))
            .ToArray();

        return Task.FromResult<IReadOnlyList<WeatherForecast>>(forecast);
    }
}
