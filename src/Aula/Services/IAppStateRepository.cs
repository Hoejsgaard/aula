using System.Threading.Tasks;

namespace Aula.Services;

public interface IAppStateRepository
{
    Task<string?> GetAppStateAsync(string key);
    Task SetAppStateAsync(string key, string value);
}