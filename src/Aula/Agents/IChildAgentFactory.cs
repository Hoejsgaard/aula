using Aula.Configuration;
using Aula.Scheduling;

namespace Aula.Agents;

/// <summary>
/// Factory interface for creating ChildAgent instances with proper dependency injection.
/// </summary>
public interface IChildAgentFactory
{
    /// <summary>
    /// Creates a new ChildAgent instance for the specified child.
    /// </summary>
    /// <param name="child">The child configuration for which to create the agent.</param>
    /// <param name="schedulingService">The scheduling service instance to inject.</param>
    /// <returns>A configured ChildAgent instance ready to be started.</returns>
    IChildAgent CreateChildAgent(Child child, ISchedulingService schedulingService);
}