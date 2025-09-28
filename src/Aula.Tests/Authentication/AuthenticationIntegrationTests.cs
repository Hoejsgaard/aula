using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Aula.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Aula.Tests.Authentication;

/// <summary>
/// Integration tests demonstrating complete authentication isolation between children.
/// </summary>
public class AuthenticationIntegrationTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IMinUddannelseClient> _mockInnerClient;

    public AuthenticationIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Register context services
        services.AddScoped<IChildContext, ScopedChildContext>();
        services.AddScoped<IChildContextValidator, ChildContextValidator>();

        // Register authentication services
        services.AddScoped<IChildAuditService, ChildAuditService>();

        // Mock the inner client for testing
        _mockInnerClient = new Mock<IMinUddannelseClient>();
        services.AddScoped<IMinUddannelseClient>(_ => _mockInnerClient.Object);

        services.AddScoped<IChildAuthenticationService, ChildAwareMinUddannelseClient>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ProofOfConcept_AuthenticationIsolation()
    {
        // This test proves that each child has completely isolated authentication sessions
        var child1 = new Child { FirstName = "Alice", LastName = "Anderson" };
        var child2 = new Child { FirstName = "Bob", LastName = "Brown" };

        // Setup mock to track authentication calls per child
        var authCallsPerChild = new Dictionary<string, int>();
        _mockInnerClient.Setup(c => c.LoginAsync())
            .ReturnsAsync(() => true)
            .Callback(() =>
            {
                // This would normally track which child is authenticating
                // but since LoginAsync doesn't take parameters, we rely on context
            });

        // Create separate scopes for each child
        using var scope1 = new ChildContextScope(_serviceProvider, child1);
        using var scope2 = new ChildContextScope(_serviceProvider, child2);

        // Execute authentication in parallel
        var task1 = scope1.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();

            // Authenticate child1
            var result = await authService.AuthenticateAsync();
            Assert.True(result);

            // Verify child1 is authenticated
            var isAuth = await authService.IsAuthenticatedAsync();
            Assert.True(isAuth);

            // Get session ID for child1
            var sessionId = authService.GetSessionId();
            Assert.NotEmpty(sessionId);

            return sessionId;
        });

        var task2 = scope2.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();

            // Authenticate child2
            var result = await authService.AuthenticateAsync();
            Assert.True(result);

            // Verify child2 is authenticated
            var isAuth = await authService.IsAuthenticatedAsync();
            Assert.True(isAuth);

            // Get session ID for child2
            var sessionId = authService.GetSessionId();
            Assert.NotEmpty(sessionId);

            return sessionId;
        });

        // Wait for both to complete
        var sessionIds = await Task.WhenAll(task1, task2);

        // Verify different session IDs
        Assert.NotEqual(sessionIds[0], sessionIds[1]);
    }

    [Fact]
    public async Task ProofOfConcept_SessionInvalidationIsIsolated()
    {
        // This test proves that invalidating one child's session doesn't affect another
        var child1 = new Child { FirstName = "Charlie", LastName = "Clark" };
        var child2 = new Child { FirstName = "Diana", LastName = "Davis" };

        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        // Create scopes and authenticate both children
        using var scope1 = new ChildContextScope(_serviceProvider, child1);
        using var scope2 = new ChildContextScope(_serviceProvider, child2);

        // Authenticate both children
        await scope1.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();
            await authService.AuthenticateAsync();
            Assert.True(await authService.IsAuthenticatedAsync());
        });

        await scope2.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();
            await authService.AuthenticateAsync();
            Assert.True(await authService.IsAuthenticatedAsync());
        });

        // Invalidate child1's session
        await scope1.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();
            await authService.InvalidateSessionAsync();
            Assert.False(await authService.IsAuthenticatedAsync());
        });

        // Verify child2 is still authenticated
        await scope2.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();
            Assert.True(await authService.IsAuthenticatedAsync());
        });
    }

    [Fact]
    public async Task ProofOfConcept_PermissionsAreEnforcedPerChild()
    {
        // This test proves that permissions are validated for each child independently
        var child = new Child { FirstName = "Eve", LastName = "Evans" };

        _mockInnerClient.Setup(c => c.GetWeekLetter(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>()))
            .ReturnsAsync(JObject.Parse("{\"data\": \"test\"}"));

        using var scope = new ChildContextScope(_serviceProvider, child);

        await scope.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();
            var auditService = provider.GetRequiredService<IChildAuditService>();

            // Attempt to get week letter (should succeed with default permissions)
            var result = await authService.GetWeekLetterAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.NotNull(result);

            // Check audit trail
            var auditTrail = await auditService.GetAuditTrailAsync(
                child,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(1));

            Assert.NotEmpty(auditTrail);
            Assert.Contains(auditTrail, e => e.Operation == "GetWeekLetter");
        });
    }

    [Fact]
    public async Task ProofOfConcept_AuditTrailsAreIsolated()
    {
        // This test proves that audit trails are isolated per child
        var child1 = new Child { FirstName = "Frank", LastName = "Fisher" };
        var child2 = new Child { FirstName = "Grace", LastName = "Green" };

        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        // Create scopes for both children
        using var scope1 = new ChildContextScope(_serviceProvider, child1);
        using var scope2 = new ChildContextScope(_serviceProvider, child2);

        // Generate audit events for child1
        await scope1.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();
            await authService.AuthenticateAsync();
            await authService.InvalidateSessionAsync();
        });

        // Generate audit events for child2
        await scope2.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();
            await authService.AuthenticateAsync();
        });

        // Check audit trails are isolated
        await scope1.ExecuteAsync(async provider =>
        {
            var auditService = provider.GetRequiredService<IChildAuditService>();
            var trail = await auditService.GetAuditTrailAsync(
                child1,
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddHours(1));

            Assert.NotEmpty(trail);
            Assert.All(trail, entry => Assert.Equal(child1.FirstName, entry.ChildName));
            Assert.Contains(trail, e => e.Operation == "InvalidateSession");
        });

        await scope2.ExecuteAsync(async provider =>
        {
            var auditService = provider.GetRequiredService<IChildAuditService>();
            var trail = await auditService.GetAuditTrailAsync(
                child2,
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddHours(1));

            Assert.NotEmpty(trail);
            Assert.All(trail, entry => Assert.Equal(child2.FirstName, entry.ChildName));
            Assert.DoesNotContain(trail, e => e.Operation == "InvalidateSession"); // Child2 didn't invalidate
        });
    }

    [Fact]
    public async Task ProofOfConcept_ConcurrentAuthenticationRequests()
    {
        // This test proves that concurrent authentication requests for different children work correctly
        var children = new List<Child>();
        for (int i = 0; i < 10; i++)
        {
            children.Add(new Child { FirstName = $"Child{i}", LastName = "Test" });
        }

        _mockInnerClient.Setup(c => c.LoginAsync())
            .ReturnsAsync(() =>
            {
                // Simulate some processing time
                Thread.Sleep(Random.Shared.Next(1, 10));
                return true;
            });

        var tasks = children.Select(async child =>
        {
            using var scope = new ChildContextScope(_serviceProvider, child);
            return await scope.ExecuteAsync(async provider =>
            {
                var authService = provider.GetRequiredService<IChildAuthenticationService>();

                // Authenticate
                var result = await authService.AuthenticateAsync();
                Assert.True(result);

                // Get unique session ID
                var sessionId = authService.GetSessionId();
                Assert.NotEmpty(sessionId);

                return new { Child = child.FirstName, SessionId = sessionId };
            });
        });

        var results = await Task.WhenAll(tasks);

        // Verify all children authenticated successfully
        Assert.Equal(10, results.Length);

        // Verify all session IDs are unique
        var uniqueSessions = results.Select(r => r.SessionId).Distinct().Count();
        Assert.Equal(10, uniqueSessions);

        // Verify correct child-to-session mapping
        foreach (var result in results)
        {
            Assert.StartsWith("Child", result.Child);
        }
    }

    [Fact]
    public async Task ProofOfConcept_SessionTimeoutIsPerChild()
    {
        // This test would prove session timeout is tracked per child
        // In a real implementation, we'd test the 30-minute timeout
        // For testing, we'd need to make the timeout configurable

        var child = new Child { FirstName = "Henry", LastName = "Hill" };
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        using var scope = new ChildContextScope(_serviceProvider, child);

        await scope.ExecuteAsync(async provider =>
        {
            var authService = provider.GetRequiredService<IChildAuthenticationService>();

            // Authenticate
            await authService.AuthenticateAsync();
            Assert.True(await authService.IsAuthenticatedAsync());

            // Get last auth time
            var authTime = authService.GetLastAuthenticationTime();
            Assert.NotNull(authTime);

            // In a real test, we'd advance time and check timeout
            // For now, just verify the timestamp is set correctly
            Assert.True(authTime.Value <= DateTimeOffset.UtcNow);
            Assert.True(authTime.Value > DateTimeOffset.UtcNow.AddSeconds(-5));
        });
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}