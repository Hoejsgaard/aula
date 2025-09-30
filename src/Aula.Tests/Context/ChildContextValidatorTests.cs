using Aula.Configuration;
using Aula.Context;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Context;

public class ChildContextValidatorTests
{
	private readonly Mock<ILogger<ChildContextValidator>> _mockLogger;
	private readonly ChildContextValidator _validator;
	private readonly Child _testChild;
	private readonly Mock<IChildContext> _mockContext;

	public ChildContextValidatorTests()
	{
		_mockLogger = new Mock<ILogger<ChildContextValidator>>();
		_validator = new ChildContextValidator(_mockLogger.Object);
		_testChild = new Child { FirstName = "Test", LastName = "Child" };
		_mockContext = new Mock<IChildContext>();
	}

	[Fact]
	public async Task ValidateContextIntegrityAsync_WithValidContext_ReturnsTrue()
	{
		// Arrange
		_mockContext.Setup(c => c.CurrentChild).Returns(_testChild);
		_mockContext.Setup(c => c.ContextId).Returns(Guid.NewGuid());
		_mockContext.Setup(c => c.CreatedAt).Returns(DateTimeOffset.UtcNow);

		// Act
		var result = await _validator.ValidateContextIntegrityAsync(_mockContext.Object);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public async Task ValidateContextIntegrityAsync_WithNullContext_ReturnsFalse()
	{
		// Act
		var result = await _validator.ValidateContextIntegrityAsync(null!);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateContextIntegrityAsync_WithNoChild_ReturnsFalse()
	{
		// Arrange
		_mockContext.Setup(c => c.CurrentChild).Returns((Child?)null);
		_mockContext.Setup(c => c.ContextId).Returns(Guid.NewGuid());
		_mockContext.Setup(c => c.CreatedAt).Returns(DateTimeOffset.UtcNow);

		// Act
		var result = await _validator.ValidateContextIntegrityAsync(_mockContext.Object);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateContextIntegrityAsync_WithEmptyGuid_ReturnsFalse()
	{
		// Arrange
		_mockContext.Setup(c => c.CurrentChild).Returns(_testChild);
		_mockContext.Setup(c => c.ContextId).Returns(Guid.Empty);
		_mockContext.Setup(c => c.CreatedAt).Returns(DateTimeOffset.UtcNow);

		// Act
		var result = await _validator.ValidateContextIntegrityAsync(_mockContext.Object);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateContextIntegrityAsync_WithFutureTimestamp_ReturnsFalse()
	{
		// Arrange
		_mockContext.Setup(c => c.CurrentChild).Returns(_testChild);
		_mockContext.Setup(c => c.ContextId).Returns(Guid.NewGuid());
		_mockContext.Setup(c => c.CreatedAt).Returns(DateTimeOffset.UtcNow.AddMinutes(5));

		// Act
		var result = await _validator.ValidateContextIntegrityAsync(_mockContext.Object);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateChildPermissionsAsync_WithValidOperation_ReturnsTrue()
	{
		// Arrange
		var validOperations = new[]
		{
			"read:week_letter",
			"read:week_schedule",
			"read:calendar",
			"write:reminder",
			"read:reminder",
			"delete:reminder",
			"send:message",
			"read:conversation"
		};

		// Act & Assert
		foreach (var operation in validOperations)
		{
			var result = await _validator.ValidateChildPermissionsAsync(_testChild, operation);
			Assert.True(result, $"Operation {operation} should be valid");
		}
	}

	[Fact]
	public async Task ValidateChildPermissionsAsync_WithInvalidOperation_ReturnsFalse()
	{
		// Act
		var result = await _validator.ValidateChildPermissionsAsync(_testChild, "invalid:operation");

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateChildPermissionsAsync_WithNullChild_ReturnsFalse()
	{
		// Act
		var result = await _validator.ValidateChildPermissionsAsync(null!, "read:week_letter");

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateChildPermissionsAsync_WithNullOperation_ReturnsFalse()
	{
		// Act
		var result = await _validator.ValidateChildPermissionsAsync(_testChild, null!);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateChildPermissionsAsync_WithEmptyOperation_ReturnsFalse()
	{
		// Act
		var result = await _validator.ValidateChildPermissionsAsync(_testChild, "");

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task ValidateChildPermissionsAsync_IsCaseInsensitive()
	{
		// Act
		var result1 = await _validator.ValidateChildPermissionsAsync(_testChild, "READ:WEEK_LETTER");
		var result2 = await _validator.ValidateChildPermissionsAsync(_testChild, "Read:Week_Letter");

		// Assert
		Assert.True(result1);
		Assert.True(result2);
	}

	[Fact]
	public void ValidateContextMatchesChild_WithMatchingChild_ReturnsTrue()
	{
		// Arrange
		_mockContext.Setup(c => c.CurrentChild).Returns(_testChild);

		// Act
		var result = _validator.ValidateContextMatchesChild(_mockContext.Object, _testChild);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void ValidateContextMatchesChild_WithDifferentChild_ReturnsFalse()
	{
		// Arrange
		_mockContext.Setup(c => c.CurrentChild).Returns(_testChild);
		var differentChild = new Child { FirstName = "Different", LastName = "Child" };

		// Act
		var result = _validator.ValidateContextMatchesChild(_mockContext.Object, differentChild);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ValidateContextMatchesChild_WithNullContext_ReturnsFalse()
	{
		// Act
		var result = _validator.ValidateContextMatchesChild(null!, _testChild);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ValidateContextMatchesChild_WithNullExpectedChild_ReturnsFalse()
	{
		// Arrange
		_mockContext.Setup(c => c.CurrentChild).Returns(_testChild);

		// Act
		var result = _validator.ValidateContextMatchesChild(_mockContext.Object, null!);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ValidateContextLifetime_WithinLimit_ReturnsTrue()
	{
		// Arrange
		_mockContext.Setup(c => c.CreatedAt).Returns(DateTimeOffset.UtcNow.AddMinutes(-5));
		var maxLifetime = TimeSpan.FromMinutes(10);

		// Act
		var result = _validator.ValidateContextLifetime(_mockContext.Object, maxLifetime);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void ValidateContextLifetime_ExceedsLimit_ReturnsFalse()
	{
		// Arrange
		_mockContext.Setup(c => c.CreatedAt).Returns(DateTimeOffset.UtcNow.AddMinutes(-15));
		var maxLifetime = TimeSpan.FromMinutes(10);

		// Act
		var result = _validator.ValidateContextLifetime(_mockContext.Object, maxLifetime);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ValidateContextLifetime_WithNullContext_ReturnsFalse()
	{
		// Act
		var result = _validator.ValidateContextLifetime(null!, TimeSpan.FromMinutes(10));

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void ValidateContextLifetime_AtExactLimit_ReturnsTrue()
	{
		// Arrange
		// Use slightly less than 10 minutes to avoid timing issues
		var exactTime = DateTimeOffset.UtcNow.AddMinutes(-9.99);
		_mockContext.Setup(c => c.CreatedAt).Returns(exactTime);
		var maxLifetime = TimeSpan.FromMinutes(10);

		// Act
		var result = _validator.ValidateContextLifetime(_mockContext.Object, maxLifetime);

		// Assert
		Assert.True(result); // Should be valid at near the limit
	}
}
