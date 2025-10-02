namespace Aula.Content.WeekLetters;

public interface IWeekLetterSeeder
{
    Task SeedTestDataAsync();
    Task SeedWeekLetterAsync(string childName, int weekNumber, int year, string content, string? className = null);
}
