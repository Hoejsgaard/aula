using Aula.Configuration;
using Aula.Context;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Context;

public class ScopedChildContextTests
{
	private readonly Mock<ILogger<ScopedChildContext>> _mockLogger;
	private readonly ScopedChildContext _context;
	private readonly Child _testChild;

	public ScopedChildContextTests()
	{
		_mockLogger = new Mock<ILogger<ScopedChildContext>>();
		_context = new ScopedChildContext(_mockLogger.Object);
		_testChild = new Child { FirstName = "Test", LastName = "Child" };
	}

	[Fact]
	public void Constructor_InitializesPropertiesCorrectly()
	{
		// Assert
		Assert.Null(_context.CurrentChild);
		Assert.NotEqual(Guid.Empty, _context.ContextId);
		Assert.True(_context.CreatedAt <= DateTimeOffset.UtcNow);
		Assert.True(_context.CreatedAt > DateTimeOffset.UtcNow.AddSeconds(-5));
	}

	[Fact]
	public void SetChild_WithValidChild_SetsCurrentChild()
	{
		// Act
		_context.SetChild(_testChild);

		// Assert
		Assert.Equal(_testChild, _context.CurrentChild);
		Assert.Equal("Test", _context.CurrentChild!.FirstName);
	}

	[Fact]
	public void SetChild_WithNull_ThrowsArgumentNullException()
	{
		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => _context.SetChild(null!));
	}

	[Fact]
	public void SetChild_WhenAlreadySet_ThrowsInvalidOperationException()
	{
		// Arrange
		_context.SetChild(_testChild);
		var anotherChild = new Child { FirstName = "Another", LastName = "Child" };

		// Act & Assert
		var ex = Assert.Throws<InvalidOperationException>(() => _context.SetChild(anotherChild));
		Assert.Contains("already set", ex.Message);
		Assert.Contains("immutable", ex.Message);
	}

	[Fact]
	public void ClearChild_RemovesCurrentChild()
	{
		// Arrange
		_context.SetChild(_testChild);

		// Act
		_context.ClearChild();

		// Assert
		Assert.Null(_context.CurrentChild);
	}

	[Fact]
	public void ValidateContext_WithNoChild_ThrowsInvalidOperationException()
	{
		// Act & Assert
		var ex = Assert.Throws<InvalidOperationException>(() => _context.ValidateContext());
		Assert.Contains("No child context set", ex.Message);
	}

	[Fact]
	public void ValidateContext_WithChild_DoesNotThrow()
	{
		// Arrange
		_context.SetChild(_testChild);

		// Act & Assert - Should not throw
		_context.ValidateContext();
	}

	[Fact]
	public void Dispose_ClearsChild()
	{
		// Arrange
		_context.SetChild(_testChild);

		// Act
		_context.Dispose();

		// Assert - After disposal, accessing should throw
		Assert.Throws<ObjectDisposedException>(() => _context.CurrentChild);
	}

	[Fact]
	public void Dispose_MultipleCalls_DoesNotThrow()
	{
		// Act & Assert - Should not throw
		_context.Dispose();
		_context.Dispose();
	}

	[Fact]
	public void AccessAfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		_context.Dispose();

		// Act & Assert
		Assert.Throws<ObjectDisposedException>(() => _context.CurrentChild);
		Assert.Throws<ObjectDisposedException>(() => _context.SetChild(_testChild));
		Assert.Throws<ObjectDisposedException>(() => _context.ValidateContext());
	}

	[Fact]
	public void MultipleScopedContexts_AreIndependent()
	{
		// Arrange
		var context1 = new ScopedChildContext(_mockLogger.Object);
		var context2 = new ScopedChildContext(_mockLogger.Object);
		var child1 = new Child { FirstName = "Child1", LastName = "Test" };
		var child2 = new Child { FirstName = "Child2", LastName = "Test" };

		// Act
		context1.SetChild(child1);
		context2.SetChild(child2);

		// Assert
		Assert.Equal("Child1", context1.CurrentChild?.FirstName);
		Assert.Equal("Child2", context2.CurrentChild?.FirstName);
		Assert.NotEqual(context1.ContextId, context2.ContextId);

		// Cleanup
		context1.Dispose();
		context2.Dispose();
	}

	[Fact]
	public void ContextId_IsUniquePerInstance()
	{
		// Arrange
		var contexts = new List<ScopedChildContext>();
		var contextIds = new HashSet<Guid>();

		// Act
		for (int i = 0; i < 100; i++)
		{
			var context = new ScopedChildContext(_mockLogger.Object);
			contexts.Add(context);
			contextIds.Add(context.ContextId);
		}

		// Assert
		Assert.Equal(100, contextIds.Count); // All IDs should be unique

		// Cleanup
		contexts.ForEach(c => c.Dispose());
	}

	[Fact]
	public void SetChild_LogsInformation()
	{
		// Act
		_context.SetChild(_testChild);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Set child context")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void SetChild_WhenAlreadySet_LogsError()
	{
		// Arrange
		_context.SetChild(_testChild);
		var anotherChild = new Child { FirstName = "Another", LastName = "Child" };

		// Act
		Assert.Throws<InvalidOperationException>(() => _context.SetChild(anotherChild));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Attempted to set child")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}
