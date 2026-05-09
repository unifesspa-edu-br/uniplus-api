namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

public sealed class SchemaRegistrySettingsValidatorTests
{
    [Fact]
    public void Validate_UrlVazia_DeveSerSucesso()
    {
        // Feature off em Development sem Apicurio — nada a validar.
        SchemaRegistrySettings settings = new();
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("ftp://schema-registry/")]
    [InlineData("not-a-url")]
    [InlineData("//missing-scheme")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1054:URI-like parameters should not be strings",
        Justification = "Test theory parameter — entrada por InlineData; bind via IConfiguration usa string sempre.")]
    public void Validate_UrlNaoHttpHttps_DeveFalhar(string url)
    {
        SchemaRegistrySettings settings = new() { Url = url };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("SchemaRegistry:Url", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AuthTypeInvalido_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            AuthType = "MTLS",
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("AuthType", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("")]
    public void Validate_AuthTypeNoneSemCampos_DeveSerSucesso(string authType)
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            AuthType = authType,
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_BasicSemUserInfo_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            AuthType = "Basic",
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("BasicAuthUserInfo", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BasicComUserInfoSemColon_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            AuthType = "Basic",
            BasicAuthUserInfo = "userwithoutcolon",
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("user:password", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BasicCompleto_DeveSerSucesso()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            AuthType = "Basic",
            BasicAuthUserInfo = "admin:s3cr3t",
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_OAuthBearerSemTokenEndpoint_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "https://schema-registry/apis/ccompat/v7",
            AuthType = "OAuthBearer",
            OAuth = new OAuthBearerSettings
            {
                ClientId = "uniplus-api-selecao",
                ClientSecret = "secret",
            },
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("OAuth:TokenEndpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OAuthBearerSemClientId_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "https://schema-registry/apis/ccompat/v7",
            AuthType = "OAuthBearer",
            OAuth = new OAuthBearerSettings
            {
                TokenEndpoint = "https://kc/realms/uniplus/protocol/openid-connect/token",
                ClientSecret = "secret",
            },
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("OAuth:ClientId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OAuthBearerSemClientSecret_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "https://schema-registry/apis/ccompat/v7",
            AuthType = "OAuthBearer",
            OAuth = new OAuthBearerSettings
            {
                TokenEndpoint = "https://kc/realms/uniplus/protocol/openid-connect/token",
                ClientId = "uniplus-api-selecao",
            },
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("OAuth:ClientSecret", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OAuthBearerCompleto_DeveSerSucesso()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "https://schema-registry/apis/ccompat/v7",
            AuthType = "OAuthBearer",
            OAuth = new OAuthBearerSettings
            {
                TokenEndpoint = "https://kc/realms/uniplus/protocol/openid-connect/token",
                ClientId = "uniplus-api-selecao",
                ClientSecret = "very-secret",
            },
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_BasicAuthFieldComAuthTypeNone_DeveFalhar()
    {
        // Coerência: BasicAuthUserInfo preenchido mas AuthType=None faz config
        // ficar inerte silenciosamente — falha explícita orienta o operador.
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            AuthType = "None",
            BasicAuthUserInfo = "user:pwd",
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("BasicAuthUserInfo só faz sentido", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OAuthFieldComAuthTypeBasic_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            AuthType = "Basic",
            BasicAuthUserInfo = "user:pwd",
            OAuth = new OAuthBearerSettings
            {
                ClientId = "stale-config",
            },
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("OAuth:* só faz sentido", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RequestTimeoutNaoPositivo_DeveFalhar()
    {
        SchemaRegistrySettings settings = new()
        {
            Url = "http://localhost:8081",
            RequestTimeoutMs = 0,
        };
        ValidateOptionsResult result = new SchemaRegistrySettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("RequestTimeoutMs", StringComparison.Ordinal));
    }
}
