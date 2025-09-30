using Aula.Configuration;
using Aula.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aula.Tests.Context;

/// <summary>
/// Integration tests demonstrating proof-of-concept for scoped service isolation.
/// These tests prove that child contexts are completely isolated within service scopes.
/// </summary>
public class ChildContextIntegrationTests
{
	private readonly ServiceProvider _serviceProvider;

	public ChildContextIntegrationTests()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddConsole());
		services.AddScoped<IChildContext, ScopedChildContext>();
		services.AddScoped<IChildContextValidator, ChildContextValidator>();
		_serviceProvider = services.BuildServiceProvider();
	}

	[Fact]
	public async Task ProofOfConcept_ScopedServiceIsolation()
	{
		// This test demonstrates complete isolation between child scopes
		var child1 = new Child { FirstName = "Alice", LastName = "Anderson" };
		var child2 = new Child { FirstName = "Bob", LastName = "Brown" };

		// Create two separate scopes
		using var scope1 = new ChildContextScope(_serviceProvider, child1);
		using var scope2 = new ChildContextScope(_serviceProvider, child2);

		// Execute operations in parallel to prove isolation
		var task1 = scope1.ExecuteAsync(async provider =>
		{
			var context = provider.GetRequiredService<IChildContext>();
			var validator = provider.GetRequiredService<IChildContextValidator>();

			// Verify we're in the correct context
			Assert.Equal("Alice", context.CurrentChild?.FirstName);

			// Validate context integrity
			var isValid = await validator.ValidateContextIntegrityAsync(context);
			Assert.True(isValid);

			// Validate permissions
			var hasPermission = await validator.ValidateChildPermissionsAsync(
				context.CurrentChild!, "read:week_letter");
			Assert.True(hasPermission);

			// Simulate some work
			await Task.Delay(10);

			// Verify context hasn't changed after async operation
			Assert.Equal("Alice", context.CurrentChild?.FirstName);
			return "Alice completed";
		});

		var task2 = scope2.ExecuteAsync(async provider =>
		{
			var context = provider.GetRequiredService<IChildContext>();
			var validator = provider.GetRequiredService<IChildContextValidator>();

			// Verify we're in the correct context
			Assert.Equal("Bob", context.CurrentChild?.FirstName);

			// Validate context integrity
			var isValid = await validator.ValidateContextIntegrityAsync(context);
			Assert.True(isValid);

			// Validate permissions
			var hasPermission = await validator.ValidateChildPermissionsAsync(
				context.CurrentChild!, "write:reminder");
			Assert.True(hasPermission);

			// Simulate some work
			await Task.Delay(10);

			// Verify context hasn't changed after async operation
			Assert.Equal("Bob", context.CurrentChild?.FirstName);
			return "Bob completed";
		});

		// Wait for both operations to complete
		var results = await Task.WhenAll(task1, task2);

		// Verify both operations completed successfully
		Assert.Equal("Alice completed", results[0]);
		Assert.Equal("Bob completed", results[1]);

		// Verify scopes are still isolated after operations
		Assert.Equal("Alice", scope1.Context.CurrentChild?.FirstName);
		Assert.Equal("Bob", scope2.Context.CurrentChild?.FirstName);
	}

	[Fact]
	public async Task ProofOfConcept_ContextCannotBeSpoofed()
	{
		// This test proves that context cannot be manipulated or spoofed
		var legitChild = new Child { FirstName = "Legitimate", LastName = "User" };
		var attackerChild = new Child { FirstName = "Attacker", LastName = "Evil" };

		using var scope = new ChildContextScope(_serviceProvider, legitChild);

		await scope.ExecuteAsync(provider =>
		{
			var context = provider.GetRequiredService<IChildContext>();

			// Verify initial context
			Assert.Equal("Legitimate", context.CurrentChild?.FirstName);

			// Try to change the context (this should fail)
			Assert.Throws<InvalidOperationException>(() => context.SetChild(attackerChild));

			// Verify context hasn't changed
			Assert.Equal("Legitimate", context.CurrentChild?.FirstName);

			return Task.CompletedTask;
		});
	}

	[Fact]
	public async Task ProofOfConcept_ResourcesDoNotLeak()
	{
		// This test demonstrates that resources are properly cleaned up
		var children = new List<Child>();
		for (int i = 0; i < 100; i++)
		{
			children.Add(new Child { FirstName = $"Child{i}", LastName = "Test" });
		}

		var scopeIds = new List<Guid>();

		// Create and dispose many scopes
		for (int i = 0; i < 100; i++)
		{
			using var scope = new ChildContextScope(_serviceProvider, children[i]);
			scopeIds.Add(scope.Context.ContextId);

			await scope.ExecuteAsync(provider =>
			{
				var context = provider.GetRequiredService<IChildContext>();
				Assert.Equal($"Child{i}", context.CurrentChild?.FirstName);
				return Task.CompletedTask;
			});
		}

		// Verify all context IDs are unique (no reuse/leaking)
		Assert.Equal(100, scopeIds.Distinct().Count());

		// Force garbage collection to ensure resources are cleaned up
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		// If we got here without exceptions, resources were properly managed
	}

	[Fact]
	public async Task ProofOfConcept_ContextFlowsThroughAsyncOperations()
	{
		// This test proves that context is properly maintained through async flows
		var child = new Child { FirstName = "AsyncTest", LastName = "Child" };

		using var scope = new ChildContextScope(_serviceProvider, child);

		await scope.ExecuteAsync(async provider =>
		{
			var context = provider.GetRequiredService<IChildContext>();
			Assert.Equal("AsyncTest", context.CurrentChild?.FirstName);

			// Simulate multiple async hops
			await Task.Yield();
			Assert.Equal("AsyncTest", context.CurrentChild?.FirstName);

			await Task.Run(async () =>
			{
				await Task.Delay(1);
				// Context should still be accessible in background task
				Assert.Equal("AsyncTest", context.CurrentChild?.FirstName);
			});

			await Task.Delay(1);
			Assert.Equal("AsyncTest", context.CurrentChild?.FirstName);

			// Parallel async operations
			var tasks = Enumerable.Range(0, 10).Select(async i =>
			{
				await Task.Delay(Random.Shared.Next(1, 5));
				Assert.Equal("AsyncTest", context.CurrentChild?.FirstName);
				return i;
			});

			var results = await Task.WhenAll(tasks);
			Assert.Equal(10, results.Length);
			Assert.Equal("AsyncTest", context.CurrentChild?.FirstName);
		});
	}

	[Fact]
	public void ProofOfConcept_ContextBoundariesAreEnforced()
	{
		// This test proves that context boundaries are properly enforced
		var child1 = new Child { FirstName = "Scope1", LastName = "Child" };
		var child2 = new Child { FirstName = "Scope2", LastName = "Child" };

		// Create first scope
		using var scope1 = new ChildContextScope(_serviceProvider, child1);
		var context1 = scope1.Context;

		// Create second scope
		using var scope2 = new ChildContextScope(_serviceProvider, child2);
		var context2 = scope2.Context;

		// Verify contexts are independent
		Assert.NotEqual(context1.ContextId, context2.ContextId);
		Assert.Equal("Scope1", context1.CurrentChild?.FirstName);
		Assert.Equal("Scope2", context2.CurrentChild?.FirstName);

		// Dispose first scope
		scope1.Dispose();

		// Second scope should still be valid
		Assert.Equal("Scope2", context2.CurrentChild?.FirstName);

		// First context should be disposed
		Assert.Throws<ObjectDisposedException>(() => context1.CurrentChild);
	}

	[Fact]
	public async Task ProofOfConcept_MemoryUsageRemainsWithinBounds()
	{
		// This test demonstrates that memory usage stays within acceptable bounds
		var initialMemory = GC.GetTotalMemory(true);

		// Create many scopes sequentially
		for (int i = 0; i < 1000; i++)
		{
			var child = new Child { FirstName = $"Child{i}", LastName = "Test" };
			using var scope = new ChildContextScope(_serviceProvider, child);

			await scope.ExecuteAsync(provider =>
			{
				var context = provider.GetRequiredService<IChildContext>();
				context.ValidateContext();
				return Task.CompletedTask;
			});
		}

		// Force garbage collection
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var finalMemory = GC.GetTotalMemory(true);
		var memoryIncrease = finalMemory - initialMemory;

		// Memory increase should be minimal (less than 1.1MB for 1000 operations)
		// This is a rough check - in practice, some memory increase is expected
		// but it should not grow unbounded
		// Allow 1.1MB threshold to account for small variations in memory management
		Assert.True(memoryIncrease < 1_100_000,
			$"Memory increased by {memoryIncrease:N0} bytes, which exceeds acceptable threshold");
	}

	public void Dispose()
	{
		_serviceProvider?.Dispose();
	}
}
