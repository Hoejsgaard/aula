using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using System.Threading.Tasks;

namespace MinUddannelse.Repositories;

public interface IAppStateRepository
{
    Task<string?> GetAppStateAsync(string key);
    Task SetAppStateAsync(string key, string value);
}
