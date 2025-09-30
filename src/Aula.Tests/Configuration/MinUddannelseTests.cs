using Aula.Configuration;
using System.Collections.Generic;
using Xunit;

namespace Aula.Tests.Configuration;

public class MinUddannelseTests
{
	[Fact]
	public void Constructor_InitializesEmptyChildrenList()
	{
		// Act
		var minUddannelse = new MinUddannelse();

		// Assert
		Assert.NotNull(minUddannelse.Children);
		Assert.Empty(minUddannelse.Children);
		Assert.IsType<List<Child>>(minUddannelse.Children);
	}

	[Fact]
	public void Children_CanSetAndGetValue()
	{
		// Arrange
		var minUddannelse = new MinUddannelse();
		var children = new List<Child>
		{
			new Child { FirstName = "Emma", LastName = "Test" },
			new Child { FirstName = "Lucas", LastName = "Test" }
		};

		// Act
		minUddannelse.Children = children;

		// Assert
		Assert.Equal(children, minUddannelse.Children);
		Assert.Equal(2, minUddannelse.Children.Count);
		Assert.Equal("Emma", minUddannelse.Children[0].FirstName);
		Assert.Equal("Lucas", minUddannelse.Children[1].FirstName);
	}

	[Fact]
	public void Children_CanBeModifiedAfterConstruction()
	{
		// Arrange
		var minUddannelse = new MinUddannelse();
		var child = new Child { FirstName = "Emma", LastName = "Test" };

		// Act
		minUddannelse.Children.Add(child);

		// Assert
		Assert.Single(minUddannelse.Children);
		Assert.Equal(child, minUddannelse.Children[0]);
	}

	[Fact]
	public void Children_CanBeSetToNull()
	{
		// Arrange
		var minUddannelse = new MinUddannelse();

		// Act
		minUddannelse.Children = null!;

		// Assert
		Assert.Null(minUddannelse.Children);
	}

	[Fact]
	public void Children_SupportsLargeNumberOfChildren()
	{
		// Arrange
		var minUddannelse = new MinUddannelse();
		var children = new List<Child>();
		for (int i = 0; i < 100; i++)
		{
			children.Add(new Child { FirstName = $"Child{i}", LastName = "Test" });
		}

		// Act
		minUddannelse.Children = children;

		// Assert
		Assert.Equal(100, minUddannelse.Children.Count);
		Assert.Equal("Child0", minUddannelse.Children[0].FirstName);
		Assert.Equal("Child99", minUddannelse.Children[99].FirstName);
	}

	[Fact]
	public void MinUddannelse_HasCorrectNamespace()
	{
		// Arrange
		var type = typeof(MinUddannelse);

		// Act & Assert
		Assert.Equal("Aula.Configuration", type.Namespace);
	}

	[Fact]
	public void MinUddannelse_IsPublicClass()
	{
		// Arrange
		var type = typeof(MinUddannelse);

		// Act & Assert
		Assert.True(type.IsPublic);
		Assert.False(type.IsAbstract);
		Assert.False(type.IsSealed);
	}

	[Fact]
	public void MinUddannelse_HasCorrectProperties()
	{
		// Arrange
		var type = typeof(MinUddannelse);

		// Act
		var childrenProperty = type.GetProperty("Children");

		// Assert
		Assert.NotNull(childrenProperty);
		Assert.True(childrenProperty.CanRead);
		Assert.True(childrenProperty.CanWrite);
		Assert.Equal(typeof(List<Child>), childrenProperty.PropertyType);
	}

	[Fact]
	public void MinUddannelse_HasParameterlessConstructor()
	{
		// Arrange
		var type = typeof(MinUddannelse);

		// Act
		var constructor = type.GetConstructor(System.Type.EmptyTypes);

		// Assert
		Assert.NotNull(constructor);
		Assert.True(constructor.IsPublic);
	}

	[Fact]
	public void Children_ListOperationsWork()
	{
		// Arrange
		var minUddannelse = new MinUddannelse();
		var child1 = new Child { FirstName = "Emma", LastName = "Test" };
		var child2 = new Child { FirstName = "Lucas", LastName = "Test" };

		// Act & Assert - Add
		minUddannelse.Children.Add(child1);
		Assert.Single(minUddannelse.Children);

		// Act & Assert - Add another
		minUddannelse.Children.Add(child2);
		Assert.Equal(2, minUddannelse.Children.Count);

		// Act & Assert - Remove
		minUddannelse.Children.Remove(child1);
		Assert.Single(minUddannelse.Children);
		Assert.Equal(child2, minUddannelse.Children[0]);

		// Act & Assert - Clear
		minUddannelse.Children.Clear();
		Assert.Empty(minUddannelse.Children);
	}
}
