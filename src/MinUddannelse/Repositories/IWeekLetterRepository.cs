using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MinUddannelse.Repositories;

public interface IWeekLetterRepository
{
    Task<bool> HasWeekLetterBeenPostedAsync(string childName, int weekNumber, int year);
    Task MarkWeekLetterAsPostedAsync(string childName, int weekNumber, int year, string contentHash, bool postedToSlack = false, bool postedToTelegram = false);
    Task StoreWeekLetterAsync(string childName, int weekNumber, int year, string contentHash, string rawContent, bool postedToSlack = false, bool postedToTelegram = false);
    Task<string?> GetStoredWeekLetterAsync(string childName, int weekNumber, int year);
    Task<List<StoredWeekLetter>> GetStoredWeekLettersAsync(string? childName = null, int? year = null);
    Task<StoredWeekLetter?> GetLatestStoredWeekLetterAsync(string childName);
    Task DeleteWeekLetterAsync(string childName, int weekNumber, int year);
}
