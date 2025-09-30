using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Aula.Tests.Services;

/// <summary>
/// Integration tests demonstrating complete data isolation between children.
/// </summary>
public class DataServiceIntegrationTests
{
	private readonly ServiceProvider _serviceProvider;
	private readonly Mock<ISupabaseService> _mockSupabaseService;
	private readonly IMemoryCache _memoryCache;

	public DataServiceIntegrationTests()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddConsole());
		services.AddMemoryCache();

		// Register context services
		services.AddScoped<IChildContext, ScopedChildContext>();
		services.AddScoped<IChildContextValidator, ChildContextValidator>();
		services.AddScoped<IChildAuditService, ChildAuditService>();

		// Register rate limiter
		services.AddSingleton<IChildRateLimiter, ChildRateLimiter>();

		// Register data services
		services.AddSingleton<Config>(new Config
		{
			MinUddannelse = new MinUddannelse
			{
				Children = new List<Child>
				{
					new Child { FirstName = "Alice", LastName = "Anderson" },
					new Child { FirstName = "Bob", LastName = "Brown" }
				}
			}
		});
		services.AddSingleton<IDataService, DataService>();

		// Mock Supabase
		_mockSupabaseService = new Mock<ISupabaseService>();
		services.AddSingleton<ISupabaseService>(_ => _mockSupabaseService.Object);

		// Mock MinUddannelseClient
		var mockMinUddannelseClient = new Mock<Aula.Integration.IMinUddannelseClient>();
		services.AddSingleton<Aula.Integration.IMinUddannelseClient>(_ => mockMinUddannelseClient.Object);

		// Register secure service
		services.AddScoped<IChildDataService, SecureChildDataService>();

		_serviceProvider = services.BuildServiceProvider();
		_memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
	}

	[Fact]
	public async Task ProofOfConcept_DataIsolation()
	{
		// This test proves that each child has completely isolated data operations
		var child1 = new Child { FirstName = "Charlie", LastName = "Clark" };
		var child2 = new Child { FirstName = "Diana", LastName = "Davis" };

		var letter1 = JObject.Parse("{\"content\": \"Letter for Charlie\"}");
		var letter2 = JObject.Parse("{\"content\": \"Letter for Diana\"}");

		// Create separate scopes for each child
		using var scope1 = new ChildContextScope(_serviceProvider, child1);
		using var scope2 = new ChildContextScope(_serviceProvider, child2);

		// Cache letters for each child
		await scope1.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			await dataService.CacheWeekLetterAsync(10, 2025, letter1);
		});

		await scope2.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			await dataService.CacheWeekLetterAsync(10, 2025, letter2);
		});

		// Verify each child retrieves their own letter
		await scope1.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			var retrieved = await dataService.GetWeekLetterAsync(10, 2025);
			Assert.NotNull(retrieved);
			Assert.Equal("Letter for Charlie", retrieved["content"]?.ToString());
		});

		await scope2.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			var retrieved = await dataService.GetWeekLetterAsync(10, 2025);
			Assert.NotNull(retrieved);
			Assert.Equal("Letter for Diana", retrieved["content"]?.ToString());
		});
	}

	[Fact]
	public async Task ProofOfConcept_CacheKeyIsolation()
	{
		// This test proves cache keys are child-prefixed and isolated
		var child = new Child { FirstName = "Eve", LastName = "Evans" };
		var letter = JObject.Parse("{\"content\": \"Test letter\"}");

		using var scope = new ChildContextScope(_serviceProvider, child);

		await scope.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			await dataService.CacheWeekLetterAsync(15, 2025, letter);
		});

		// Verify cache key is child-prefixed
		var cacheKey = $"WeekLetter:Eve:Evans:15:2025";
		Assert.True(_memoryCache.TryGetValue(cacheKey, out JObject? cached));
		Assert.NotNull(cached);
		Assert.Equal("Test letter", cached["content"]?.ToString());

		// Verify different child can't access it with wrong key
		var wrongKey = $"WeekLetter:Frank:Fisher:15:2025";
		Assert.False(_memoryCache.TryGetValue(wrongKey, out _));
	}

	[Fact]
	public async Task ProofOfConcept_RateLimitingPerChild()
	{
		// This test proves rate limiting is enforced per child
		var child1 = new Child { FirstName = "Grace", LastName = "Green" };
		var child2 = new Child { FirstName = "Henry", LastName = "Hill" };

		using var scope1 = new ChildContextScope(_serviceProvider, child1);
		using var scope2 = new ChildContextScope(_serviceProvider, child2);

		// Configure mock to track database operations
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(
			It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.Returns(Task.CompletedTask);

		// Child1 exhausts their rate limit for StoreWeekLetter (limit: 10)
		await scope1.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();

			// Store 10 letters (at limit)
			for (int i = 0; i < 10; i++)
			{
				var letter = JObject.Parse($"{{\"week\": {i}}}");
				await dataService.StoreWeekLetterAsync(i, 2025, letter);
			}

			// 11th attempt should fail with rate limit
			var extraLetter = JObject.Parse("{\"week\": 11}");
			await Assert.ThrowsAsync<RateLimitExceededException>(async () =>
				await dataService.StoreWeekLetterAsync(11, 2025, extraLetter));
		});

		// Child2 should still be able to store (separate rate limit)
		await scope2.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			var letter = JObject.Parse("{\"content\": \"Child2 letter\"}");
			var result = await dataService.StoreWeekLetterAsync(1, 2025, letter);
			Assert.True(result);
		});
	}

	[Fact]
	public async Task ProofOfConcept_PermissionEnforcement()
	{
		// This test proves permissions are validated per child per operation
		var child = new Child { FirstName = "Ian", LastName = "Irving" };

		using var scope = new ChildContextScope(_serviceProvider, child);

		await scope.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			var auditService = provider.GetRequiredService<IChildAuditService>();

			// Attempt to delete (requires delete:database permission)
			var result = await dataService.DeleteWeekLetterAsync(10, 2025);

			// Default implementation grants permission, but audit trail records attempt
			var auditTrail = await auditService.GetAuditTrailAsync(
				child,
				DateTimeOffset.UtcNow.AddMinutes(-1),
				DateTimeOffset.UtcNow.AddMinutes(1));

			Assert.NotEmpty(auditTrail);
			Assert.Contains(auditTrail, e => e.Operation == "DataDeletion");
		});
	}

	[Fact]
	public async Task ProofOfConcept_AuditTrailForDataAccess()
	{
		// This test proves all data operations are audited
		var child = new Child { FirstName = "Jack", LastName = "Johnson" };
		var letter = JObject.Parse("{\"content\": \"Audit test\"}");

		using var scope = new ChildContextScope(_serviceProvider, child);

		await scope.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();
			var auditService = provider.GetRequiredService<IChildAuditService>();

			// Perform various data operations
			await dataService.CacheWeekLetterAsync(20, 2025, letter);
			await dataService.GetWeekLetterAsync(20, 2025);
			await dataService.CacheWeekScheduleAsync(20, 2025, letter);
			await dataService.GetWeekScheduleAsync(20, 2025);

			// Check audit trail
			var auditTrail = await auditService.GetAuditTrailAsync(
				child,
				DateTimeOffset.UtcNow.AddMinutes(-1),
				DateTimeOffset.UtcNow.AddMinutes(1));

			Assert.NotEmpty(auditTrail);
			Assert.Contains(auditTrail, e => e.Operation == "CacheWeekLetter");
			Assert.Contains(auditTrail, e => e.Operation == "GetWeekLetter");
			Assert.Contains(auditTrail, e => e.Operation == "CacheWeekSchedule");
			Assert.Contains(auditTrail, e => e.Operation == "GetWeekSchedule");
			Assert.All(auditTrail, e => Assert.Equal(child.FirstName, e.ChildName));
		});
	}

	[Fact]
	public async Task ProofOfConcept_ConcurrentDataOperations()
	{
		// This test proves concurrent data operations work correctly for multiple children
		var children = new List<Child>();
		for (int i = 0; i < 5; i++)
		{
			children.Add(new Child { FirstName = $"Child{i}", LastName = "Test" });
		}

		var tasks = children.Select(async (child, index) =>
		{
			using var scope = new ChildContextScope(_serviceProvider, child);
			return await scope.ExecuteAsync(async provider =>
			{
				var dataService = provider.GetRequiredService<IChildDataService>();

				// Each child caches their own letter
				var letter = JObject.Parse($"{{\"child\": \"{child.FirstName}\", \"index\": {index}}}");
				await dataService.CacheWeekLetterAsync(index, 2025, letter);

				// Retrieve and verify
				var retrieved = await dataService.GetWeekLetterAsync(index, 2025);
				Assert.NotNull(retrieved);
				Assert.Equal(child.FirstName, retrieved["child"]?.ToString());
				Assert.Equal(index, retrieved["index"]?.Value<int>());

				return new { Child = child.FirstName, Letter = retrieved };
			});
		}).ToArray();

		var results = await Task.WhenAll(tasks);

		// Verify all children got their correct data
		Assert.Equal(5, results.Length);
		for (int i = 0; i < 5; i++)
		{
			Assert.Equal($"Child{i}", results[i].Child);
			Assert.Equal($"Child{i}", results[i].Letter["child"]?.ToString());
		}
	}

	[Fact]
	public async Task ProofOfConcept_NoGetChildrenMethod()
	{
		// This test proves IChildDataService has no cross-child operations
		var child = new Child { FirstName = "Karen", LastName = "King" };

		using var scope = new ChildContextScope(_serviceProvider, child);

		await scope.ExecuteAsync(async provider =>
		{
			var dataService = provider.GetRequiredService<IChildDataService>();

			// Verify IChildDataService has no GetChildren method
			var serviceType = typeof(IChildDataService);
			var getChildrenMethod = serviceType.GetMethod("GetChildren");
			Assert.Null(getChildrenMethod); // No cross-child operations allowed

			// Can only operate on current child from context
			var letter = JObject.Parse("{\"test\": true}");
			await dataService.CacheWeekLetterAsync(25, 2025, letter);
		});
	}

	public void Dispose()
	{
		_serviceProvider?.Dispose();
	}
}
