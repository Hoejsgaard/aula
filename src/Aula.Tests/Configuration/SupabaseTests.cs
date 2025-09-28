using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class SupabaseTests
{
    [Fact]
    public void Constructor_InitializesEmptyStrings()
    {
        // Act
        var supabase = new Aula.Configuration.Supabase();

        // Assert
        Assert.NotNull(supabase.Url);
        Assert.NotNull(supabase.Key);
        Assert.NotNull(supabase.ServiceRoleKey);
        Assert.Equal(string.Empty, supabase.Url);
        Assert.Equal(string.Empty, supabase.Key);
        Assert.Equal(string.Empty, supabase.ServiceRoleKey);
    }

    [Fact]
    public void Url_CanSetAndGetValue()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();
        var testUrl = "https://test.supabase.co";

        // Act
        supabase.Url = testUrl;

        // Assert
        Assert.Equal(testUrl, supabase.Url);
    }

    [Fact]
    public void Key_CanSetAndGetValue()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();
        var testKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test";

        // Act
        supabase.Key = testKey;

        // Assert
        Assert.Equal(testKey, supabase.Key);
    }

    [Fact]
    public void ServiceRoleKey_CanSetAndGetValue()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();
        var testServiceRoleKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.service";

        // Act
        supabase.ServiceRoleKey = testServiceRoleKey;

        // Assert
        Assert.Equal(testServiceRoleKey, supabase.ServiceRoleKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.supabase.co")]
    [InlineData("https://my-project.supabase.co")]
    [InlineData("http://localhost:3000")]
    [InlineData("https://staging.company.com")]
    public void Url_AcceptsVariousFormats(string url)
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();

        // Act
        supabase.Url = url;

        // Assert
        Assert.Equal(url, supabase.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("simple_key")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InRlc3QiLCJyb2xlIjoiYW5vbiIsImlhdCI6MTY0NjA2NzA2MywiZXhwIjoxOTYxNjQzMDYzfQ.test")]
    [InlineData("very_long_key_with_many_characters_1234567890")]
    public void Key_AcceptsVariousFormats(string key)
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();

        // Act
        supabase.Key = key;

        // Assert
        Assert.Equal(key, supabase.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("service_role_key")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.service_role")]
    [InlineData("very_long_service_role_key_with_many_characters")]
    public void ServiceRoleKey_AcceptsVariousFormats(string serviceRoleKey)
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();

        // Act
        supabase.ServiceRoleKey = serviceRoleKey;

        // Assert
        Assert.Equal(serviceRoleKey, supabase.ServiceRoleKey);
    }

    [Fact]
    public void Url_CanBeSetToNull()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();

        // Act
        supabase.Url = null!;

        // Assert
        Assert.Null(supabase.Url);
    }

    [Fact]
    public void Key_CanBeSetToNull()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();

        // Act
        supabase.Key = null!;

        // Assert
        Assert.Null(supabase.Key);
    }

    [Fact]
    public void ServiceRoleKey_CanBeSetToNull()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();

        // Act
        supabase.ServiceRoleKey = null!;

        // Assert
        Assert.Null(supabase.ServiceRoleKey);
    }

    [Fact]
    public void AllProperties_CanBeSetSimultaneously()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();
        var url = "https://test.supabase.co";
        var key = "test_key";
        var serviceRoleKey = "test_service_role_key";

        // Act
        supabase.Url = url;
        supabase.Key = key;
        supabase.ServiceRoleKey = serviceRoleKey;

        // Assert
        Assert.Equal(url, supabase.Url);
        Assert.Equal(key, supabase.Key);
        Assert.Equal(serviceRoleKey, supabase.ServiceRoleKey);
    }

    [Fact]
    public void Supabase_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(Aula.Configuration.Supabase);

        // Act & Assert
        Assert.Equal("Aula.Configuration", type.Namespace);
    }

    [Fact]
    public void Supabase_IsPublicClass()
    {
        // Arrange
        var type = typeof(Aula.Configuration.Supabase);

        // Act & Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void Supabase_HasCorrectProperties()
    {
        // Arrange
        var type = typeof(Aula.Configuration.Supabase);

        // Act
        var urlProperty = type.GetProperty("Url");
        var keyProperty = type.GetProperty("Key");
        var serviceRoleKeyProperty = type.GetProperty("ServiceRoleKey");

        // Assert
        Assert.NotNull(urlProperty);
        Assert.NotNull(keyProperty);
        Assert.NotNull(serviceRoleKeyProperty);

        Assert.True(urlProperty.CanRead);
        Assert.True(urlProperty.CanWrite);
        Assert.Equal(typeof(string), urlProperty.PropertyType);

        Assert.True(keyProperty.CanRead);
        Assert.True(keyProperty.CanWrite);
        Assert.Equal(typeof(string), keyProperty.PropertyType);

        Assert.True(serviceRoleKeyProperty.CanRead);
        Assert.True(serviceRoleKeyProperty.CanWrite);
        Assert.Equal(typeof(string), serviceRoleKeyProperty.PropertyType);
    }

    [Fact]
    public void Supabase_HasParameterlessConstructor()
    {
        // Arrange
        var type = typeof(Aula.Configuration.Supabase);

        // Act
        var constructor = type.GetConstructor(System.Type.EmptyTypes);

        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void Supabase_PropertiesAreIndependent()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();

        // Act
        supabase.Url = "changed_url";
        // Other properties should remain unchanged

        // Assert
        Assert.Equal("changed_url", supabase.Url);
        Assert.Equal(string.Empty, supabase.Key);
        Assert.Equal(string.Empty, supabase.ServiceRoleKey);
    }

    [Fact]
    public void Supabase_ObjectInitializerSyntaxWorks()
    {
        // Arrange & Act
        var supabase = new Aula.Configuration.Supabase
        {
            Url = "https://init.supabase.co",
            Key = "init_key",
            ServiceRoleKey = "init_service_key"
        };

        // Assert
        Assert.Equal("https://init.supabase.co", supabase.Url);
        Assert.Equal("init_key", supabase.Key);
        Assert.Equal("init_service_key", supabase.ServiceRoleKey);
    }

    [Fact]
    public void Supabase_SupportsTypicalSupabaseUrlPattern()
    {
        // Arrange
        var supabase = new Aula.Configuration.Supabase();
        var typicalUrl = "https://abcdefghijklmnop.supabase.co";

        // Act
        supabase.Url = typicalUrl;

        // Assert
        Assert.Equal(typicalUrl, supabase.Url);
        Assert.Contains("supabase.co", supabase.Url);
    }
}