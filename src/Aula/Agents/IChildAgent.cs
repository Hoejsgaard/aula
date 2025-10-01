using System.Threading.Tasks;

namespace Aula.Agents;

public interface IChildAgent
{
    Task StartAsync();
    Task StopAsync();
}
