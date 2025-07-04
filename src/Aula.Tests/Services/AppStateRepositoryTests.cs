using Microsoft.Extensions.Logging;
using Aula.Services;
using System;
using Xunit;

namespace Aula.Tests.Services;

public class AppStateRepositoryTests
{
    private readonly ILoggerFactory _loggerFactory;

    public AppStateRepositoryTests()
    {
        _loggerFactory = new LoggerFactory();
    }

    [Fact]
    public void Constructor_WithNullSupabase_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new AppStateRepository(null!, _loggerFactory));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new AppStateRepository(null!, null!));
    }

    [Fact]
    public void Repository_ImplementsIAppStateRepositoryInterface()
    {
        // Arrange & Act & Assert
        Assert.True(typeof(IAppStateRepository).IsAssignableFrom(typeof(AppStateRepository)));
    }

    [Fact]
    public void Repository_HasCorrectPublicMethods()
    {
        // Arrange
        var repositoryType = typeof(AppStateRepository);

        // Act & Assert
        Assert.NotNull(repositoryType.GetMethod("GetAppStateAsync"));
        Assert.NotNull(repositoryType.GetMethod("SetAppStateAsync"));
    }

    [Fact]
    public void Repository_HasCorrectConstructorParameters()
    {
        // Arrange
        var repositoryType = typeof(AppStateRepository);
        var constructor = repositoryType.GetConstructors()[0];

        // Act
        var parameters = constructor.GetParameters();

        // Assert
        Assert.Equal(2, parameters.Length);
        Assert.Equal("supabase", parameters[0].Name);
        Assert.Equal("loggerFactory", parameters[1].Name);
    }

    [Fact]
    public void Repository_PublicMethodsHaveCorrectSignatures()
    {
        // Arrange
        var repositoryType = typeof(AppStateRepository);

        // Act
        var getMethod = repositoryType.GetMethod("GetAppStateAsync");
        var setMethod = repositoryType.GetMethod("SetAppStateAsync");

        // Assert
        Assert.NotNull(getMethod);
        Assert.NotNull(setMethod);
        Assert.Single(getMethod.GetParameters());
        Assert.Equal(2, setMethod.GetParameters().Length);
        Assert.Equal(typeof(string), getMethod.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(string), setMethod.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(string), setMethod.GetParameters()[1].ParameterType);
    }

    [Fact]
    public void AppStateRepository_HasCorrectNamespace()
    {
        // Arrange
        var repositoryType = typeof(AppStateRepository);

        // Act & Assert
        Assert.Equal("Aula.Services", repositoryType.Namespace);
    }

    [Fact]
    public void AppStateRepository_IsPublicClass()
    {
        // Arrange
        var repositoryType = typeof(AppStateRepository);

        // Act & Assert
        Assert.True(repositoryType.IsPublic);
        Assert.False(repositoryType.IsAbstract);
        Assert.False(repositoryType.IsSealed);
    }
}