using Microsoft.Extensions.Logging;

namespace Aula.Services;

public interface IHistoricalDataSeeder
{
    Task SeedHistoricalWeekLettersAsync();
}