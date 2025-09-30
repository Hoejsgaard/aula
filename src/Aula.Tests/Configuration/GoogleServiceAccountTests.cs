using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class GoogleServiceAccountTests
{
	[Fact]
	public void Constructor_InitializesEmptyStrings()
	{
		// Act
		var account = new GoogleServiceAccount();

		// Assert
		Assert.NotNull(account.Type);
		Assert.NotNull(account.ProjectId);
		Assert.NotNull(account.PrivateKeyId);
		Assert.NotNull(account.PrivateKey);
		Assert.NotNull(account.ClientEmail);
		Assert.NotNull(account.ClientId);
		Assert.NotNull(account.AuthUri);
		Assert.NotNull(account.TokenUri);
		Assert.NotNull(account.AuthProviderX509CertUrl);
		Assert.NotNull(account.ClientX509CertUrl);
		Assert.NotNull(account.UniverseDomain);

		Assert.Equal(string.Empty, account.Type);
		Assert.Equal(string.Empty, account.ProjectId);
		Assert.Equal(string.Empty, account.PrivateKeyId);
		Assert.Equal(string.Empty, account.PrivateKey);
		Assert.Equal(string.Empty, account.ClientEmail);
		Assert.Equal(string.Empty, account.ClientId);
		Assert.Equal(string.Empty, account.AuthUri);
		Assert.Equal(string.Empty, account.TokenUri);
		Assert.Equal(string.Empty, account.AuthProviderX509CertUrl);
		Assert.Equal(string.Empty, account.ClientX509CertUrl);
		Assert.Equal(string.Empty, account.UniverseDomain);
	}

	[Fact]
	public void Type_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testType = "service_account";

		// Act
		account.Type = testType;

		// Assert
		Assert.Equal(testType, account.Type);
	}

	[Fact]
	public void ProjectId_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testProjectId = "my-project-123";

		// Act
		account.ProjectId = testProjectId;

		// Assert
		Assert.Equal(testProjectId, account.ProjectId);
	}

	[Fact]
	public void PrivateKeyId_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testPrivateKeyId = "1234567890abcdef";

		// Act
		account.PrivateKeyId = testPrivateKeyId;

		// Assert
		Assert.Equal(testPrivateKeyId, account.PrivateKeyId);
	}

	[Fact]
	public void PrivateKey_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testPrivateKey = "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC1234567890";

		// Act
		account.PrivateKey = testPrivateKey;

		// Assert
		Assert.Equal(testPrivateKey, account.PrivateKey);
	}

	[Fact]
	public void ClientEmail_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testClientEmail = "service@my-project.iam.gserviceaccount.com";

		// Act
		account.ClientEmail = testClientEmail;

		// Assert
		Assert.Equal(testClientEmail, account.ClientEmail);
	}

	[Fact]
	public void ClientId_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testClientId = "123456789012345678901";

		// Act
		account.ClientId = testClientId;

		// Assert
		Assert.Equal(testClientId, account.ClientId);
	}

	[Fact]
	public void AuthUri_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testAuthUri = "https://accounts.google.com/o/oauth2/auth";

		// Act
		account.AuthUri = testAuthUri;

		// Assert
		Assert.Equal(testAuthUri, account.AuthUri);
	}

	[Fact]
	public void TokenUri_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testTokenUri = "https://oauth2.googleapis.com/token";

		// Act
		account.TokenUri = testTokenUri;

		// Assert
		Assert.Equal(testTokenUri, account.TokenUri);
	}

	[Fact]
	public void AuthProviderX509CertUrl_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testUrl = "https://www.googleapis.com/oauth2/v1/certs";

		// Act
		account.AuthProviderX509CertUrl = testUrl;

		// Assert
		Assert.Equal(testUrl, account.AuthProviderX509CertUrl);
	}

	[Fact]
	public void ClientX509CertUrl_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testUrl = "https://www.googleapis.com/robot/v1/metadata/x509/service%40my-project.iam.gserviceaccount.com";

		// Act
		account.ClientX509CertUrl = testUrl;

		// Assert
		Assert.Equal(testUrl, account.ClientX509CertUrl);
	}

	[Fact]
	public void UniverseDomain_CanSetAndGetValue()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var testDomain = "googleapis.com";

		// Act
		account.UniverseDomain = testDomain;

		// Assert
		Assert.Equal(testDomain, account.UniverseDomain);
	}

	[Fact]
	public void AllProperties_CanBeSetSimultaneously()
	{
		// Arrange
		var account = new GoogleServiceAccount();
		var type = "service_account";
		var projectId = "my-project";
		var privateKeyId = "123key";
		var privateKey = "-----BEGIN PRIVATE KEY-----";
		var clientEmail = "test@project.iam.gserviceaccount.com";
		var clientId = "123456789";
		var authUri = "https://accounts.google.com/o/oauth2/auth";
		var tokenUri = "https://oauth2.googleapis.com/token";
		var authProviderUrl = "https://www.googleapis.com/oauth2/v1/certs";
		var clientCertUrl = "https://www.googleapis.com/robot/v1/metadata/x509/test";
		var universeDomain = "googleapis.com";

		// Act
		account.Type = type;
		account.ProjectId = projectId;
		account.PrivateKeyId = privateKeyId;
		account.PrivateKey = privateKey;
		account.ClientEmail = clientEmail;
		account.ClientId = clientId;
		account.AuthUri = authUri;
		account.TokenUri = tokenUri;
		account.AuthProviderX509CertUrl = authProviderUrl;
		account.ClientX509CertUrl = clientCertUrl;
		account.UniverseDomain = universeDomain;

		// Assert
		Assert.Equal(type, account.Type);
		Assert.Equal(projectId, account.ProjectId);
		Assert.Equal(privateKeyId, account.PrivateKeyId);
		Assert.Equal(privateKey, account.PrivateKey);
		Assert.Equal(clientEmail, account.ClientEmail);
		Assert.Equal(clientId, account.ClientId);
		Assert.Equal(authUri, account.AuthUri);
		Assert.Equal(tokenUri, account.TokenUri);
		Assert.Equal(authProviderUrl, account.AuthProviderX509CertUrl);
		Assert.Equal(clientCertUrl, account.ClientX509CertUrl);
		Assert.Equal(universeDomain, account.UniverseDomain);
	}

	[Fact]
	public void GoogleServiceAccount_ObjectInitializerSyntaxWorks()
	{
		// Arrange & Act
		var account = new GoogleServiceAccount
		{
			Type = "service_account",
			ProjectId = "test-project",
			PrivateKeyId = "key123",
			PrivateKey = "-----BEGIN PRIVATE KEY-----",
			ClientEmail = "test@test-project.iam.gserviceaccount.com",
			ClientId = "123456789",
			AuthUri = "https://accounts.google.com/o/oauth2/auth",
			TokenUri = "https://oauth2.googleapis.com/token",
			AuthProviderX509CertUrl = "https://www.googleapis.com/oauth2/v1/certs",
			ClientX509CertUrl = "https://www.googleapis.com/robot/v1/metadata/x509/test",
			UniverseDomain = "googleapis.com"
		};

		// Assert
		Assert.Equal("service_account", account.Type);
		Assert.Equal("test-project", account.ProjectId);
		Assert.Equal("key123", account.PrivateKeyId);
		Assert.Equal("-----BEGIN PRIVATE KEY-----", account.PrivateKey);
		Assert.Equal("test@test-project.iam.gserviceaccount.com", account.ClientEmail);
		Assert.Equal("123456789", account.ClientId);
		Assert.Equal("https://accounts.google.com/o/oauth2/auth", account.AuthUri);
		Assert.Equal("https://oauth2.googleapis.com/token", account.TokenUri);
		Assert.Equal("https://www.googleapis.com/oauth2/v1/certs", account.AuthProviderX509CertUrl);
		Assert.Equal("https://www.googleapis.com/robot/v1/metadata/x509/test", account.ClientX509CertUrl);
		Assert.Equal("googleapis.com", account.UniverseDomain);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("service_account")]
	[InlineData("authorized_user")]
	public void Type_AcceptsVariousFormats(string type)
	{
		// Arrange
		var account = new GoogleServiceAccount();

		// Act
		account.Type = type;

		// Assert
		Assert.Equal(type, account.Type);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("my-project")]
	[InlineData("test-project-123")]
	[InlineData("project-with-dashes")]
	public void ProjectId_AcceptsVariousFormats(string projectId)
	{
		// Arrange
		var account = new GoogleServiceAccount();

		// Act
		account.ProjectId = projectId;

		// Assert
		Assert.Equal(projectId, account.ProjectId);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("service@project.iam.gserviceaccount.com")]
	[InlineData("test-service@my-project-123.iam.gserviceaccount.com")]
	[InlineData("automation@company-project.iam.gserviceaccount.com")]
	public void ClientEmail_AcceptsVariousFormats(string clientEmail)
	{
		// Arrange
		var account = new GoogleServiceAccount();

		// Act
		account.ClientEmail = clientEmail;

		// Assert
		Assert.Equal(clientEmail, account.ClientEmail);
	}

	[Fact]
	public void GoogleServiceAccount_HasCorrectNamespace()
	{
		// Arrange
		var type = typeof(GoogleServiceAccount);

		// Act & Assert
		Assert.Equal("Aula.Configuration", type.Namespace);
	}

	[Fact]
	public void GoogleServiceAccount_IsPublicClass()
	{
		// Arrange
		var type = typeof(GoogleServiceAccount);

		// Act & Assert
		Assert.True(type.IsPublic);
		Assert.False(type.IsAbstract);
		Assert.False(type.IsSealed);
	}

	[Fact]
	public void GoogleServiceAccount_HasParameterlessConstructor()
	{
		// Arrange
		var type = typeof(GoogleServiceAccount);

		// Act
		var constructor = type.GetConstructor(System.Type.EmptyTypes);

		// Assert
		Assert.NotNull(constructor);
		Assert.True(constructor.IsPublic);
	}

	[Fact]
	public void GoogleServiceAccount_PropertiesAreIndependent()
	{
		// Arrange
		var account = new GoogleServiceAccount();

		// Act
		account.Type = "changed";
		// Other properties should remain unchanged

		// Assert
		Assert.Equal("changed", account.Type);
		Assert.Equal(string.Empty, account.ProjectId);
		Assert.Equal(string.Empty, account.PrivateKeyId);
		Assert.Equal(string.Empty, account.PrivateKey);
		Assert.Equal(string.Empty, account.ClientEmail);
		Assert.Equal(string.Empty, account.ClientId);
		Assert.Equal(string.Empty, account.AuthUri);
		Assert.Equal(string.Empty, account.TokenUri);
		Assert.Equal(string.Empty, account.AuthProviderX509CertUrl);
		Assert.Equal(string.Empty, account.ClientX509CertUrl);
		Assert.Equal(string.Empty, account.UniverseDomain);
	}
}
