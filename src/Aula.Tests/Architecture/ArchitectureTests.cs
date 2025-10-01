using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Architecture;

/// <summary>
/// Architecture tests to enforce the new child-centric architecture rules.
/// These tests ensure services follow the pattern: "Pass the fucking child as parameter to singletons"
/// No context patterns, no scoping - just direct Child parameters to singleton services.
/// </summary>
public class ArchitectureTests
{
    private static readonly HashSet<string> AllowedChildParameterTypes = new()
    {
		// New child-aware services that accept Child parameters directly
		"IChildDataService",
        "SecureChildDataService",
        "IChildChannelManager",
        "SecureChildChannelManager",
        "IChildScheduler",
        "SecureChildScheduler",
        "IChildAwareOpenAiService",
        "SecureChildAwareOpenAiService",
        // Renamed services from Task 011 refactoring
        "IOpenAiService",
        "IWeekLetterService",
        "IChildAuditService",
        "ChildAuditService",
        "IChildRateLimiter",
        "ChildRateLimiter",
		// Utility services that need Child parameters
		"IPromptSanitizer",
        "IMessageContentFilter",
        "IChildSchedulingRateLimiter",
		// Legacy interfaces allowed to have Child parameters (marked obsolete)
		"IDataService",
        "IAgentService",
        "IMinUddannelseClient"
    };

    private static readonly HashSet<string> LegacyInterfacesToObsolete = new()
    {
        "IDataService",
        "IAgentService",
        "IMinUddannelseClient"
    };

    private static readonly HashSet<string> RequiredChildParameterServices = new()
    {
		// These services MUST accept Child parameters
		"IChildDataService",
        "IChildChannelManager",
        "IChildScheduler",
        "IChildAwareOpenAiService"
    };

    [Fact]
    public void ChildAwareServices_Should_AcceptChildParameters()
    {
        // Arrange
        var assembly = typeof(Aula.Program).Assembly;
        var violations = new List<string>();

        // Act - Check that child-aware services accept Child parameters
        foreach (var serviceName in RequiredChildParameterServices)
        {
            var serviceType = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == serviceName);

            if (serviceType != null)
            {
                var hasChildParameter = false;
                var methods = serviceType.GetMethods();

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Any(p => p.ParameterType == typeof(Child)))
                    {
                        hasChildParameter = true;
                        break;
                    }
                }

                if (!hasChildParameter)
                {
                    violations.Add($"{serviceName} does not accept Child parameters in any method");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            var message = "Architecture violation: Child-aware services not accepting Child parameters:\n" +
                         string.Join("\n", violations) +
                         "\n\nChild-aware services must accept Child parameters directly. No context patterns allowed.";
            Assert.Fail(message);
        }
    }

    [Fact]
    public void NonAllowedServices_Should_NotHaveChildParameters()
    {
        // Arrange
        var assembly = typeof(Aula.Program).Assembly;
        var violations = new List<string>();

        // Act
        var serviceInterfaces = assembly.GetTypes()
            .Where(t => t.IsInterface)
            .Where(t => t.Namespace != null &&
                       (t.Namespace.Contains("Services") ||
                        t.Namespace.Contains("Integration") ||
                        t.Namespace.Contains("Channels") ||
                        t.Namespace.Contains("Scheduling")))
            .Where(t => !AllowedChildParameterTypes.Contains(t.Name));

        foreach (var interfaceType in serviceInterfaces)
        {
            var methods = interfaceType.GetMethods();
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                foreach (var param in parameters)
                {
                    if (param.ParameterType == typeof(Child))
                    {
                        violations.Add($"{interfaceType.Name}.{method.Name} has Child parameter '{param.Name}'");
                    }
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            var message = "Architecture violation: The following methods have Child parameters but are not allowed:\n" +
                         string.Join("\n", violations) +
                         "\n\nOnly approved child-aware services should accept Child parameters.";
            Assert.Fail(message);
        }
    }

    [Fact]
    public void LegacyInterfaces_Should_BeMarkedObsolete()
    {
        // Arrange
        var assembly = typeof(Aula.Program).Assembly;
        var violations = new List<string>();

        // Act
        foreach (var interfaceName in LegacyInterfacesToObsolete)
        {
            var interfaceType = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == interfaceName);

            if (interfaceType != null)
            {
                var obsoleteAttr = interfaceType.GetCustomAttribute<ObsoleteAttribute>();
                if (obsoleteAttr == null)
                {
                    violations.Add($"{interfaceName} is not marked as [Obsolete]");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            var message = "Architecture violation: Legacy interfaces not marked obsolete:\n" +
                         string.Join("\n", violations) +
                         "\n\nLegacy interfaces must be marked with [Obsolete] attribute.";
            Assert.Fail(message);
        }
    }

    [Fact]
    public void NoService_Should_IterateOverMultipleChildren()
    {
        // Arrange
        var assembly = typeof(Aula.Program).Assembly;
        var violations = new List<string>();

        // Act
        var serviceTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Namespace != null && (t.Namespace.Contains("Services") ||
                                               t.Namespace.Contains("Channels") ||
                                               t.Namespace.Contains("Scheduling")))
            .Where(t => t.Name != "DataService") // Legacy service allowed to have GetChildren
            .Where(t => t.Name != "ChannelManager") // Channel manager operates on all channels
            .Where(t => t.Name != "SchedulingService"); // Scheduling service coordinates multiple children

        foreach (var serviceType in serviceTypes)
        {
            var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                // Check for methods that return collections of children
                var returnType = method.ReturnType;
                if (IsChildCollection(returnType))
                {
                    violations.Add($"{serviceType.Name}.{method.Name} returns a collection of Child");
                }

                // Check for parameters that are collections of children
                foreach (var param in method.GetParameters())
                {
                    if (IsChildCollection(param.ParameterType))
                    {
                        violations.Add($"{serviceType.Name}.{method.Name} accepts collection of Child");
                    }
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            var message = "Architecture violation: Services operating on multiple children:\n" +
                         string.Join("\n", violations) +
                         "\n\nServices should operate on single child only. Use singleton services with Child parameters.";
            Assert.Fail(message);
        }
    }

    [Fact]
    public void NoContextPatterns_Should_Exist()
    {
        // Arrange
        var assembly = typeof(Aula.Program).Assembly;
        var violations = new List<string>();
        var forbiddenPatterns = new[] { "Context", "Scope", "Scoped" };

        // Act - Check for any remaining context patterns
        var allTypes = assembly.GetTypes()
            .Where(t => t.Namespace != null && !t.Namespace.Contains("Tests"))
            .Where(t => forbiddenPatterns.Any(pattern => t.Name.Contains(pattern)))
            .Where(t => !t.Name.Contains("Conversation")) // ConversationContext is allowed
            .Where(t => !t.Name.StartsWith("<")) // Exclude compiler-generated types
            .ToList();

        foreach (var type in allTypes)
        {
            violations.Add($"Type '{type.Name}' contains forbidden context pattern");
        }

        // Assert
        if (violations.Any())
        {
            var message = "Architecture violation: Context patterns still exist:\n" +
                         string.Join("\n", violations) +
                         "\n\nAll context patterns should be eliminated. Use direct Child parameters instead.";
            Assert.Fail(message);
        }
    }

    [Fact]
    public void NoDirectInstantiation_Of_ChildAwareServices()
    {
        // This test ensures child-aware services are only created through DI
        // and not directly instantiated with 'new'

        var assembly = typeof(Aula.Program).Assembly;
        var violations = new List<string>();

        var secureServices = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Name.StartsWith("Secure"))
            .Select(t => t.Name)
            .ToHashSet();

        // Check all classes for direct instantiation
        var allTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract);

        foreach (var type in allTypes)
        {
            if (type.Name == "Program")
                continue; // Program class is allowed to configure DI

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (method.IsConstructor) continue;

                try
                {
                    var methodBody = method.GetMethodBody();
                    if (methodBody != null)
                    {
                        // This is a simplified check - in production, we'd use Roslyn analyzers
                        // For now, we just check if the method name suggests instantiation
                        if (method.Name.Contains("Create") && secureServices.Contains(method.ReturnType.Name))
                        {
                            violations.Add($"{type.Name}.{method.Name} might be creating {method.ReturnType.Name} directly");
                        }
                    }
                }
                catch
                {
                    // Ignore methods we can't inspect
                }
            }
        }

        // Note: This test is informational - direct instantiation should be caught at compile time
        Assert.Empty(violations);
    }

    private static bool IsChildCollection(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(Dictionary<,>) ||
                genericDef == typeof(Task<>) ||
                genericDef == typeof(ValueTask<>))
            {
                var genericArgs = type.GetGenericArguments();
                foreach (var arg in genericArgs)
                {
                    if (arg == typeof(Child) || IsChildCollection(arg))
                    {
                        return true;
                    }
                }
            }
        }

        if (type.IsArray && type.GetElementType() == typeof(Child))
        {
            return true;
        }

        return false;
    }
}
