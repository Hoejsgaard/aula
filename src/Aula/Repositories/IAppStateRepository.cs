using Aula.Models;
using Aula.Repositories.DTOs;
using Aula.Models;
using Aula.Repositories.DTOs;
using System.Threading.Tasks;

namespace Aula.Repositories;

public interface IAppStateRepository
{
    Task<string?> GetAppStateAsync(string key);
    Task SetAppStateAsync(string key, string value);
}
