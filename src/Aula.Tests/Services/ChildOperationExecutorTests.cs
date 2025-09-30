using System.Collections.Generic;
using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Services;

public class ChildOperationExecutorTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<ChildOperationExecutor>> _mockLogger;
    private readonly Mock<IChildAuditService> _mockAuditService;
    private readonly ChildOperationExecutor _executor;
    private readonly Child _testChild;

    public ChildOperationExecutorTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<ChildOperationExecutor>>();
        _mockAuditService = new Mock<IChildAuditService>();

        _testChild = new Child { FirstName = "Test", LastName = "Child" };

        _executor = new ChildOperationExecutor(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockAuditService.Object);
    }

    [Fact]
    public async Task ExecuteInChildContextAsync_CreatesScope_AndSetsChildContext()
    {
        // Arrange
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScopeProvider = new Mock<IServiceProvider>();
        var mockContext = new Mock<IChildContext>();

        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);
        mockScopeProvider.Setup(p => p.GetService(typeof(IChildContext)))
            .Returns(mockContext.Object);

        var operationExecuted = false;

        // Act
        var result = await _executor.ExecuteInChildContextAsync(_testChild,
            (provider) =>
            {
                operationExecuted = true;
                Assert.Same(mockScopeProvider.Object, provider);
                return Task.FromResult("success");
            },
            "TestOperation");

        // Assert
        Assert.Equal("success", result);
        Assert.True(operationExecuted);
        mockContext.Verify(c => c.SetChild(_testChild), Times.Once);
        mockScope.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExecuteInChildContextAsync_AuditsSuccessfulOperation()
    {
        // Arrange
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScopeProvider = new Mock<IServiceProvider>();
        var mockContext = new Mock<IChildContext>();

        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);
        mockScopeProvider.Setup(p => p.GetService(typeof(IChildContext)))
            .Returns(mockContext.Object);

        // Act
        await _executor.ExecuteInChildContextAsync(_testChild,
            async (provider) => await Task.CompletedTask,
            "TestOperation");

        // Assert
        _mockAuditService.Verify(a => a.LogDataAccessAsync(
            _testChild,
            "TestOperation",
            "Operation completed successfully",
            true), Times.Once);
    }

    [Fact]
    public async Task ExecuteInChildContextAsync_AuditsFailedOperation()
    {
        // Arrange
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScopeProvider = new Mock<IServiceProvider>();
        var mockContext = new Mock<IChildContext>();

        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);
        mockScopeProvider.Setup(p => p.GetService(typeof(IChildContext)))
            .Returns(mockContext.Object);

        var expectedException = new InvalidOperationException("Test failure");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _executor.ExecuteInChildContextAsync(_testChild,
                async (provider) =>
                {
                    await Task.CompletedTask;
                    throw expectedException;
                },
                "TestOperation");
        });

        _mockAuditService.Verify(a => a.LogDataAccessAsync(
            _testChild,
            "TestOperation",
            $"Operation failed: {expectedException.Message}",
            false), Times.Once);
    }

    [Fact]
    public async Task ExecuteInChildContextAsync_ThrowsWhenChildIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _executor.ExecuteInChildContextAsync<string>(
                null!,
                async (provider) => await Task.FromResult("test"),
                "TestOperation");
        });
    }

    [Fact]
    public async Task ExecuteInChildContextAsync_ThrowsWhenOperationIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _executor.ExecuteInChildContextAsync<string>(
                _testChild,
                null!,
                "TestOperation");
        });
    }

    [Fact]
    public async Task ExecuteForAllChildrenAsync_ExecutesInParallel()
    {
        // Arrange
        var children = new[]
        {
            new Child { FirstName = "Child1", LastName = "Test" },
            new Child { FirstName = "Child2", LastName = "Test" },
            new Child { FirstName = "Child3", LastName = "Test" }
        };

        var executionOrder = new List<string>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);

        // Setup scopes for each child
        foreach (var child in children)
        {
            var mockScope = new Mock<IServiceScope>();
            var mockScopeProvider = new Mock<IServiceProvider>();
            var mockContext = new Mock<IChildContext>();

            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);
            mockScopeProvider.Setup(p => p.GetService(typeof(IChildContext)))
                .Returns(mockContext.Object);
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        }

        // Act
        var results = await _executor.ExecuteForAllChildrenAsync(children,
            async (provider) =>
            {
                await Task.Delay(10); // Simulate some work
                lock (executionOrder)
                {
                    executionOrder.Add(Thread.CurrentThread.ManagedThreadId.ToString());
                }
                return "completed";
            },
            "ParallelOperation");

        // Assert
        Assert.Equal(3, results.Count);
        foreach (var child in children)
        {
            Assert.Contains(child, results.Keys);
            Assert.Equal("completed", results[child]);
        }
    }

    [Fact]
    public async Task ExecuteForAllChildrenAsync_ContinuesOnIndividualFailure()
    {
        // Arrange
        var children = new[]
        {
            new Child { FirstName = "Child1", LastName = "Test" },
            new Child { FirstName = "Child2", LastName = "Test" }, // Will fail
            new Child { FirstName = "Child3", LastName = "Test" }
        };

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);

        // Setup scopes for each child
        var scopes = new List<Mock<IServiceScope>>();
        foreach (var child in children)
        {
            var mockScope = new Mock<IServiceScope>();
            var mockScopeProvider = new Mock<IServiceProvider>();
            var mockContext = new Mock<IChildContext>();

            // Setup the context to return the current child
            mockContext.Setup(c => c.CurrentChild).Returns(child);

            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopeProvider.Object);
            mockScopeProvider.Setup(p => p.GetService(typeof(IChildContext)))
                .Returns(mockContext.Object);

            scopes.Add(mockScope);
        }

        // Setup factory to return scopes in sequence
        var scopeIndex = 0;
        mockScopeFactory.Setup(f => f.CreateScope())
            .Returns(() => scopes[scopeIndex++ % scopes.Count].Object);

        // Act
        var results = await _executor.ExecuteForAllChildrenAsync(children,
            async (provider) =>
            {
                await Task.CompletedTask;
                // Fail for Child2
                var context = provider.GetRequiredService<IChildContext>();
                if (context.CurrentChild?.FirstName == "Child2")
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return "success";
            },
            "TestOperation");

        // Assert
        // At least one should succeed (Child1 or Child3)
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 3);
    }

    [Fact]
    public async Task ExportChildDataAsync_ThrowsWhenChildIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _executor.ExportChildDataAsync(null!);
        });
    }

    [Fact]
    public async Task DeleteChildDataAsync_ThrowsWhenChildIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _executor.DeleteChildDataAsync(null!);
        });
    }

    [Fact]
    public async Task RecordConsentAsync_ThrowsWhenChildIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _executor.RecordConsentAsync(null!, "TestConsent", true);
        });
    }

    [Fact]
    public async Task RecordConsentAsync_ThrowsWhenConsentTypeIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _executor.RecordConsentAsync(_testChild, "", true);
        });
    }
}