namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;

using Microsoft.Extensions.Options;

/// <summary>
/// Validação cross-field de <see cref="SchemaRegistrySettings"/>.
/// </summary>
/// <remarks>
/// Garante coerência entre <c>AuthType</c> e os campos correspondentes:
/// <c>Basic</c> exige <c>BasicAuthUserInfo</c>; <c>OAuthBearer</c> exige
/// <c>OAuth.TokenEndpoint</c> + <c>OAuth.ClientId</c> + <c>OAuth.ClientSecret</c>.
/// Configurações inconsistentes falham no startup com mensagem orientada ao operador,
/// ao invés de "parecer autenticado" silenciosamente em runtime.
/// </remarks>
public sealed class SchemaRegistrySettingsValidator : IValidateOptions<SchemaRegistrySettings>
{
    private const string TipoNenhum = "None";
    private const string TipoBasic = "Basic";
    private const string TipoOAuthBearer = "OAuthBearer";

    public ValidateOptionsResult Validate(string? name, SchemaRegistrySettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // URL vazia = feature desligada (Development sem Apicurio). Nada a validar.
        if (string.IsNullOrWhiteSpace(options.Url))
        {
            return ValidateOptionsResult.Success;
        }

        List<string> falhas = FalhasDaConfiguracao(options);

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    /// <remarks>URL e tipo de autenticação inválidos encerram a validação: as demais regras dependem deles.</remarks>
    private static List<string> FalhasDaConfiguracao(SchemaRegistrySettings options)
    {
        string? recusa = FalhaDaUrl(options.Url) ?? FalhaDoTipoDeAutenticacao(options.AuthType);
        if (recusa is not null)
        {
            return [recusa];
        }

        return
        [
            .. FalhasDaAutenticacao(options),
            .. FalhasDosLimites(options),
        ];
    }

    private static IEnumerable<string> FalhasDaAutenticacao(SchemaRegistrySettings options)
    {
        string tipo = TipoDeAutenticacao(options.AuthType);
        return
        [
            .. new[]
            {
                CoerenciaDoBasic(options, tipo),
                CoerenciaDoOAuth(options, tipo),
                FalhaDoBasic(options, tipo),
            }.OfType<string>(),
            .. FalhasDoOAuth(options, tipo),
        ];
    }

    private static string TipoDeAutenticacao(string? authType) => (authType ?? string.Empty).Trim();

    private static bool EhTipo(string tipo, string esperado) => tipo.Equals(esperado, StringComparison.OrdinalIgnoreCase);

    private static bool UrlHttpAbsoluta(string valor) =>
        Uri.TryCreate(valor, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? FalhaDaUrl(string url) =>
        UrlHttpAbsoluta(url) ? null : $"SchemaRegistry:Url '{url}' inválida. Use HTTP/HTTPS absolutas.";

    private static string? FalhaDoTipoDeAutenticacao(string? authType)
    {
        string tipo = TipoDeAutenticacao(authType);
        bool reconhecido = tipo.Length == 0 || EhTipo(tipo, TipoNenhum) || EhTipo(tipo, TipoBasic) || EhTipo(tipo, TipoOAuthBearer);

        return reconhecido ? null : $"SchemaRegistry:AuthType '{authType}' inválido. Use None, Basic ou OAuthBearer.";
    }

    // Campos preenchidos exigem o AuthType correspondente: evita BasicAuthUserInfo="user:pwd" ficar inerte porque
    // AuthType=None foi o padrão.
    private static string? CoerenciaDoBasic(SchemaRegistrySettings options, string tipo) =>
        !string.IsNullOrWhiteSpace(options.BasicAuthUserInfo) && !EhTipo(tipo, TipoBasic)
            ? "SchemaRegistry:BasicAuthUserInfo só faz sentido com AuthType=Basic."
            : null;

    private static string? CoerenciaDoOAuth(SchemaRegistrySettings options, string tipo) =>
        TemCampoOAuth(options.OAuth) && !EhTipo(tipo, TipoOAuthBearer)
            ? "SchemaRegistry:OAuth:* só faz sentido com AuthType=OAuthBearer."
            : null;

    private static bool TemCampoOAuth(OAuthBearerSettings oauth) =>
        !string.IsNullOrWhiteSpace(oauth.TokenEndpoint)
        || !string.IsNullOrWhiteSpace(oauth.ClientId)
        || !string.IsNullOrWhiteSpace(oauth.ClientSecret);

    private static string? FalhaDoBasic(SchemaRegistrySettings options, string tipo)
    {
        if (!EhTipo(tipo, TipoBasic))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.BasicAuthUserInfo))
        {
            return "SchemaRegistry:BasicAuthUserInfo é obrigatório com AuthType=Basic.";
        }

        return options.BasicAuthUserInfo.Contains(':', StringComparison.Ordinal)
            ? null
            : "SchemaRegistry:BasicAuthUserInfo deve estar no formato 'user:password'.";
    }

    private static IEnumerable<string> FalhasDoOAuth(SchemaRegistrySettings options, string tipo)
    {
        OAuthBearerSettings oauth = options.OAuth;

        return !EhTipo(tipo, TipoOAuthBearer)
            ? []
            : new[]
            {
                FalhaDoEnderecoDoToken(oauth.TokenEndpoint),
                string.IsNullOrWhiteSpace(oauth.ClientId) ? "SchemaRegistry:OAuth:ClientId é obrigatório com AuthType=OAuthBearer." : null,
                string.IsNullOrWhiteSpace(oauth.ClientSecret) ? "SchemaRegistry:OAuth:ClientSecret é obrigatório com AuthType=OAuthBearer (sempre via Vault/env var)." : null,
                oauth.RefreshSkewSeconds < 0 ? "SchemaRegistry:OAuth:RefreshSkewSeconds não pode ser negativo." : null,
                oauth.RequestTimeoutMs <= 0 ? "SchemaRegistry:OAuth:RequestTimeoutMs deve ser positivo." : null,
            }.OfType<string>();
    }

    private static string? FalhaDoEnderecoDoToken(string endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco))
        {
            return "SchemaRegistry:OAuth:TokenEndpoint é obrigatório com AuthType=OAuthBearer.";
        }

        return UrlHttpAbsoluta(endereco)
            ? null
            : $"SchemaRegistry:OAuth:TokenEndpoint '{endereco}' inválido. Use HTTP/HTTPS absoluta.";
    }

    private static IEnumerable<string> FalhasDosLimites(SchemaRegistrySettings options) =>
        new[]
        {
            options.RequestTimeoutMs <= 0 ? "SchemaRegistry:RequestTimeoutMs deve ser positivo." : null,
            options.MaxCachedSchemas <= 0 ? "SchemaRegistry:MaxCachedSchemas deve ser positivo." : null,
        }.OfType<string>();
}
