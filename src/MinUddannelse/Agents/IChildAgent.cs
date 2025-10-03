using System.Threading.Tasks;
using MinUddannelse.Configuration;

namespace MinUddannelse.Agents;

public interface IChildAgent
{
    Child Child { get; }
    Task StartAsync();
    Task StopAsync();
    Task SendReminderMessageAsync(string message);
}
