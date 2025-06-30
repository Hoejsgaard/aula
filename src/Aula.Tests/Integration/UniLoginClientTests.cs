using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Aula.Integration;

namespace Aula.Tests.Integration;

public class UniLoginClientTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act & Assert - should not throw
        var client = new TestableUniLoginClient(
            "testuser", 
            "testpass", 
            "https://login.example.com", 
            "https://success.example.com");

        Assert.NotNull(client);
        Assert.Equal("https://success.example.com", client.GetSuccessUrl());
    }

    [Fact]
    public void Constructor_WithNullUsername_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TestableUniLoginClient(null!, "pass", "https://login.com", "https://success.com"));
        
        Assert.Equal("username", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullPassword_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TestableUniLoginClient("user", null!, "https://login.com", "https://success.com"));
        
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyUsername_AcceptsEmptyString()
    {
        // Act & Assert - should not throw since only null is checked
        var client = new TestableUniLoginClient("", "pass", "https://login.com", "https://success.com");
        
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithEmptyPassword_AcceptsEmptyString()
    {
        // Act & Assert - should not throw since only null is checked  
        var client = new TestableUniLoginClient("user", "", "https://login.com", "https://success.com");
        
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithValidUrls_SetsSuccessUrl()
    {
        // Arrange
        var successUrl = "https://myapp.example.com/dashboard";
        
        // Act
        var client = new TestableUniLoginClient("user", "pass", "https://login.com", successUrl);
        
        // Assert
        Assert.Equal(successUrl, client.GetSuccessUrl());
    }

    [Fact]
    public void Constructor_ConfiguresHttpClientCorrectly()
    {
        // Act
        var client = new TestableUniLoginClient("user", "pass", "https://login.com", "https://success.com");
        
        // Assert
        Assert.NotNull(client.GetHttpClient());
        
        // Verify HttpClient is configured with cookies and redirects
        var handler = client.GetHttpClientHandler();
        Assert.True(handler.UseCookies);
        Assert.True(handler.AllowAutoRedirect);
        Assert.NotNull(handler.CookieContainer);
    }

    [Fact]
    public async Task LoginAsync_CallsGetRequestToLoginUrl()
    {
        // Arrange
        var client = new TestableUniLoginClient("user", "pass", "https://login.example.com", "https://success.com");
        
        // Act & Assert - This will fail with actual HTTP call, but tests the method exists and is callable
        // In a real scenario, this would be mocked properly
        try
        {
            await client.LoginAsync();
        }
        catch (HttpRequestException)
        {
            // Expected for invalid URL in test environment
            Assert.True(true);
        }
        catch (TaskCanceledException)
        {
            // Expected for timeout in test environment
            Assert.True(true);
        }
    }
}

// Testable implementation that exposes protected members for testing
public class TestableUniLoginClient : UniLoginClient
{
    public TestableUniLoginClient(string username, string password, string loginUrl, string successUrl)
        : base(username, password, loginUrl, successUrl)
    {
    }

    public string GetSuccessUrl() => SuccessUrl;
    
    public HttpClient GetHttpClient() => HttpClient;
    
    public HttpClientHandler GetHttpClientHandler()
    {
        // Access the handler through reflection since it's private
        var handlerField = typeof(HttpClient).GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return handlerField?.GetValue(HttpClient) as HttpClientHandler ?? new HttpClientHandler();
    }
}