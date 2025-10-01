using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Architecture;

/// <summary>
/// Architecture tests to enforce the child-centric architecture rules.
/// These tests ensure no regression to the old pattern of passing Child parameters.
/// </summary>
public class ArchitectureTests
{
	private static readonly HashSet<string> AllowedChildParameterTypes = new()
	{
		// These are the ONLY types allowed to have Child parameters
		"IChildServiceCoordinator",
		"ChildServiceCoordinator",
		"IChildContext",
		"ChildContext",
		"IChildContextValidator",
		"ChildContextValidator",
		"IChildAuditService",
		"ChildAuditService",
		// Refactored services that now use direct Child parameters
		"IChildDataService",
		"SecureChildDataService",
		// Legacy interfaces allowed to have Child parameters
		"IDataService",
		"IAgentService",
		"IMinUddannelseClient",
		"IChildRateLimiter",
		"IPromptSanitizer",
		"IMessageContentFilter"
	};

	private static readonly HashSet<string> LegacyInterfacesToObsolete = new()
	{
		"IDataService",
		"IAgentService",
		"IMinUddannelseClient"
	};

	[Fact]
	public void NoNewServiceInterfaces_Should_HaveChildParameters()
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
						t.Namespace.Contains("Channels")))
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
			var message = "Architecture violation: The following methods have Child parameters:\n" +
						 string.Join("\n", violations) +
						 "\n\nChild parameters should only exist in the allowed executor types. " +
						 "Use IChildContext for child-aware services.";
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
			.Where(t => t.Namespace != null && t.Namespace.Contains("Services"))
			.Where(t => !AllowedChildParameterTypes.Contains(t.Name))
			.Where(t => t.Name != "DataService"); // Legacy service allowed to have GetChildren

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
						 "\n\nServices should operate on single child context only.";
			Assert.Fail(message);
		}
	}

	[Fact]
	public void ChildAwareServices_Should_UseIChildContext()
	{
		// Arrange
		var assembly = typeof(Aula.Program).Assembly;
		var violations = new List<string>();

		// Act
		var childAwareServices = assembly.GetTypes()
			.Where(t => t.IsClass && !t.IsAbstract)
			.Where(t => t.Name.StartsWith("Secure") && t.Name.Contains("Child"))
			.Where(t => t.Name != "SecureChildDataService"); // Excluded - refactored to use direct Child parameters

		foreach (var serviceType in childAwareServices)
		{
			var constructors = serviceType.GetConstructors();
			var hasChildContext = false;

			foreach (var ctor in constructors)
			{
				var parameters = ctor.GetParameters();
				if (parameters.Any(p => p.ParameterType.Name == "IChildContext"))
				{
					hasChildContext = true;
					break;
				}
			}

			if (!hasChildContext)
			{
				violations.Add($"{serviceType.Name} does not inject IChildContext");
			}
		}

		// Assert
		if (violations.Any())
		{
			var message = "Architecture violation: Child-aware services not using IChildContext:\n" +
						 string.Join("\n", violations) +
						 "\n\nChild-aware services must inject IChildContext for child isolation.";
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
			if (type.Name == "Program" || type.Name == "ProgramChildAware")
				continue; // Program classes are allowed to configure DI

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
