using Aula.Services;
using System.Threading.Tasks;

namespace Aula.Repositories;

public interface IRetryTrackingRepository
{
    Task<int> GetRetryAttemptsAsync(string childName, int weekNumber, int year);
    Task IncrementRetryAttemptAsync(string childName, int weekNumber, int year);
    Task MarkRetryAsSuccessfulAsync(string childName, int weekNumber, int year);
}