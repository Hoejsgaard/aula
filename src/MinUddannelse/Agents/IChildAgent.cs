using System.Threading.Tasks;

namespace MinUddannelse.Agents;

public interface IChildAgent
{
    Task StartAsync();
    Task StopAsync();
}
