using Aula.Core.Models;
using Aula.Core.Models;
using System.Threading.Tasks;

namespace Aula.Repositories;

public interface IAppStateRepository
{
    Task<string?> GetAppStateAsync(string key);
    Task SetAppStateAsync(string key, string value);
}
