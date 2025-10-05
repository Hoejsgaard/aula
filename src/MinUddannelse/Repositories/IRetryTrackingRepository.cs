using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using System.Threading.Tasks;

namespace MinUddannelse.Repositories;

public interface IRetryTrackingRepository
{
    Task<int> GetRetryAttemptsAsync(string childName, int weekNumber, int year);
    Task<bool> IncrementRetryAttemptAsync(string childName, int weekNumber, int year);
    Task MarkRetryAsSuccessfulAsync(string childName, int weekNumber, int year);
}
