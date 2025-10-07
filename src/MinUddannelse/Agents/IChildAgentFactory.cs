using MinUddannelse.Configuration;
using MinUddannelse.Scheduling;

namespace MinUddannelse.Agents;

public interface IChildAgentFactory
{
    IChildAgent CreateChildAgent(Child child, ISchedulingService schedulingService);
}
