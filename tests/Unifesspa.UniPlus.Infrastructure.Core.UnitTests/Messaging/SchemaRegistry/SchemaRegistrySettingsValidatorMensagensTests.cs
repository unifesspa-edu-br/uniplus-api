namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>Compara a lista completa e ordenada de falhas do validador do Schema Registry.</summary>
public sealed class SchemaRegistrySettingsValidatorMensagensTests
{
    private const string UrlValida = "http://schema-registry:8081";
    private const string BasicSemTipoBasic = "SchemaRegistry:BasicAuthUserInfo só faz sentido com AuthType=Basic.";
    private const string OAuthSemTipoOAuth = "SchemaRegistry:OAuth:* só faz sentido com AuthType=OAuthBearer.";
    private const string TempoLimiteNaoPositivo = "SchemaRegistry:RequestTimeoutMs deve ser positivo.";
    private const string CacheNaoPositivo = "SchemaRegistry:MaxCachedSchemas deve ser positivo.";

    private static ValidateOptionsResult Validar(SchemaRegistrySettings configuracao) =>
        new SchemaRegistrySettingsValidator().Validate(name: null, configuracao);

    private static OAuthBearerSettings OAuthCompleto() => new()
    {
        TokenEndpoint = "https://keycloak.exemplo.test/token",
        ClientId = "cliente-ficticio",
        ClientSecret = "segredo-ficticio",
    };

    private static SchemaRegistrySettings OAuthSemNadaComValoresNaoPositivos() => new()
    {
        Url = UrlValida,
        AuthType = "OAuthBearer",
        OAuth = new OAuthBearerSettings { RefreshSkewSeconds = -1, RequestTimeoutMs = 0 },
        RequestTimeoutMs = 0,
        MaxCachedSchemas = 0,
    };

    [Fact]
    public void Validate_UrlEmBrancoComAuthTypeInvalido_DevolveSucesso()
    {
        SchemaRegistrySettings configuracao = new() { Url = "   ", AuthType = "MTLS", RequestTimeoutMs = 0 };

        Validar(configuracao).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_UrlInvalidaComOutrosCamposInvalidos_DevolveSoAFalhaDaUrl()
    {
        SchemaRegistrySettings configuracao = new() { Url = "ftp://registro", AuthType = "MTLS", RequestTimeoutMs = 0 };

        Validar(configuracao).Failures.Should().Equal(
            "SchemaRegistry:Url 'ftp://registro' inválida. Use HTTP/HTTPS absolutas.");
    }

    [Fact]
    public void Validate_AuthTypeInvalidoComOutrosCamposInvalidos_DevolveSoAFalhaDoAuthTypeComOTextoInformado()
    {
        SchemaRegistrySettings configuracao = new() { Url = UrlValida, AuthType = " MTLS ", MaxCachedSchemas = 0 };

        Validar(configuracao).Failures.Should().Equal(
            "SchemaRegistry:AuthType ' MTLS ' inválido. Use None, Basic ou OAuthBearer.");
    }

    [Theory]
    [InlineData("basic")]
    [InlineData("  BASIC  ")]
    public void Validate_AuthTypeBasicEmOutraCaixaOuComEspacos_Aceita(string authType)
    {
        SchemaRegistrySettings configuracao = new() { Url = UrlValida, AuthType = authType, BasicAuthUserInfo = "u:p" };

        Validar(configuracao).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_CamposBasicEOAuthComAuthTypeNone_DevolveAsDuasCoerenciasDepoisOsLimites()
    {
        SchemaRegistrySettings configuracao = new()
        {
            Url = UrlValida,
            BasicAuthUserInfo = "u:p",
            OAuth = new OAuthBearerSettings { ClientId = "cliente-ficticio" },
            RequestTimeoutMs = 0,
            MaxCachedSchemas = -1,
        };

        Validar(configuracao).Failures.Should().Equal(
            BasicSemTipoBasic, OAuthSemTipoOAuth, TempoLimiteNaoPositivo, CacheNaoPositivo);
    }

    [Fact]
    public void Validate_BasicSemInformacaoComCampoOAuth_DevolveCoerenciaDoOAuthDepoisObrigatorioDoBasic()
    {
        SchemaRegistrySettings configuracao = new()
        {
            Url = UrlValida,
            AuthType = "Basic",
            OAuth = new OAuthBearerSettings { ClientId = "cliente-ficticio" },
        };

        Validar(configuracao).Failures.Should().Equal(
            OAuthSemTipoOAuth, "SchemaRegistry:BasicAuthUserInfo é obrigatório com AuthType=Basic.");
    }

    [Fact]
    public void Validate_BasicComInformacaoEmBranco_DevolveSoObrigatorioDoBasic()
    {
        SchemaRegistrySettings configuracao = new() { Url = UrlValida, AuthType = "Basic", BasicAuthUserInfo = "  " };

        Validar(configuracao).Failures.Should().Equal(
            "SchemaRegistry:BasicAuthUserInfo é obrigatório com AuthType=Basic.");
    }

    [Fact]
    public void Validate_BasicSemDoisPontos_DevolveSoOFormato()
    {
        SchemaRegistrySettings configuracao = new() { Url = UrlValida, AuthType = "Basic", BasicAuthUserInfo = "usuario" };

        Validar(configuracao).Failures.Should().Equal(
            "SchemaRegistry:BasicAuthUserInfo deve estar no formato 'user:password'.");
    }

    [Fact]
    public void Validate_OAuthComCampoBasic_DevolveSoACoerenciaDoBasic()
    {
        SchemaRegistrySettings configuracao = new()
        {
            Url = UrlValida,
            AuthType = "OAuthBearer",
            BasicAuthUserInfo = "u:p",
            OAuth = OAuthCompleto(),
        };

        Validar(configuracao).Failures.Should().Equal(BasicSemTipoBasic);
    }

    [Fact]
    public void Validate_OAuthSemNadaComValoresNaoPositivos_DevolveAsFalhasNaOrdemDasRegras()
    {
        Validar(OAuthSemNadaComValoresNaoPositivos()).Failures.Should().Equal(
            "SchemaRegistry:OAuth:TokenEndpoint é obrigatório com AuthType=OAuthBearer.",
            "SchemaRegistry:OAuth:ClientId é obrigatório com AuthType=OAuthBearer.",
            "SchemaRegistry:OAuth:ClientSecret é obrigatório com AuthType=OAuthBearer (sempre via Vault/env var).",
            "SchemaRegistry:OAuth:RefreshSkewSeconds não pode ser negativo.",
            "SchemaRegistry:OAuth:RequestTimeoutMs deve ser positivo.",
            TempoLimiteNaoPositivo,
            CacheNaoPositivo);
    }

    [Fact]
    public void Validate_OAuthComTokenEndpointNaoHttp_DevolveSoAFalhaDoEndereco()
    {
        SchemaRegistrySettings configuracao = new()
        {
            Url = UrlValida,
            AuthType = "OAuthBearer",
            OAuth = new OAuthBearerSettings { TokenEndpoint = "ftp://keycloak", ClientId = "c", ClientSecret = "s" },
        };

        Validar(configuracao).Failures.Should().Equal(
            "SchemaRegistry:OAuth:TokenEndpoint 'ftp://keycloak' inválido. Use HTTP/HTTPS absoluta.");
    }

    [Fact]
    public void Validate_AuthTypeNoneComSoOLimiteDoCacheNaoPositivo_DevolveSoEssaFalha()
    {
        SchemaRegistrySettings configuracao = new() { Url = UrlValida, MaxCachedSchemas = 0 };

        Validar(configuracao).Failures.Should().Equal(CacheNaoPositivo);
    }

    [Fact]
    public void Validate_OpcoesNulas_LancaArgumentNull()
    {
        Action validar = () => Validar(null!);

        validar.Should().Throw<ArgumentNullException>();
    }
}
