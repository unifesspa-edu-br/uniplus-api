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
    public ValidateOptionsResult Validate(string? name, SchemaRegistrySettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // URL vazia = feature desligada (Development sem Apicurio). Nada a validar.
        if (string.IsNullOrWhiteSpace(options.Url))
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];

        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"SchemaRegistry:Url '{options.Url}' inválida. Use HTTP/HTTPS absolutas.");
            return ValidateOptionsResult.Fail(failures);
        }

        string authType = (options.AuthType ?? string.Empty).Trim();
        bool authIsNone = authType.Length == 0
            || authType.Equals("None", StringComparison.OrdinalIgnoreCase);
        bool authIsBasic = authType.Equals("Basic", StringComparison.OrdinalIgnoreCase);
        bool authIsOAuthBearer = authType.Equals("OAuthBearer", StringComparison.OrdinalIgnoreCase);

        if (!(authIsNone || authIsBasic || authIsOAuthBearer))
        {
            failures.Add($"SchemaRegistry:AuthType '{options.AuthType}' inválido. Use None, Basic ou OAuthBearer.");
            return ValidateOptionsResult.Fail(failures);
        }

        bool hasBasicField = !string.IsNullOrWhiteSpace(options.BasicAuthUserInfo);
        bool hasOAuthField =
            !string.IsNullOrWhiteSpace(options.OAuth.TokenEndpoint)
            || !string.IsNullOrWhiteSpace(options.OAuth.ClientId)
            || !string.IsNullOrWhiteSpace(options.OAuth.ClientSecret);

        // Coerência: campos preenchidos exigem AuthType correspondente — evita
        // BasicAuthUserInfo="user:pwd" ficar inerte porque AuthType=None foi default.
        if (hasBasicField && !authIsBasic)
        {
            failures.Add("SchemaRegistry:BasicAuthUserInfo só faz sentido com AuthType=Basic.");
        }

        if (hasOAuthField && !authIsOAuthBearer)
        {
            failures.Add("SchemaRegistry:OAuth:* só faz sentido com AuthType=OAuthBearer.");
        }

        if (authIsBasic)
        {
            if (string.IsNullOrWhiteSpace(options.BasicAuthUserInfo))
            {
                failures.Add("SchemaRegistry:BasicAuthUserInfo é obrigatório com AuthType=Basic.");
            }
            else if (!options.BasicAuthUserInfo.Contains(':', StringComparison.Ordinal))
            {
                failures.Add("SchemaRegistry:BasicAuthUserInfo deve estar no formato 'user:password'.");
            }
        }

        if (authIsOAuthBearer)
        {
            if (string.IsNullOrWhiteSpace(options.OAuth.TokenEndpoint))
            {
                failures.Add("SchemaRegistry:OAuth:TokenEndpoint é obrigatório com AuthType=OAuthBearer.");
            }
            else if (!Uri.TryCreate(options.OAuth.TokenEndpoint, UriKind.Absolute, out Uri? tokenUri)
                     || (tokenUri.Scheme != Uri.UriSchemeHttp && tokenUri.Scheme != Uri.UriSchemeHttps))
            {
                failures.Add($"SchemaRegistry:OAuth:TokenEndpoint '{options.OAuth.TokenEndpoint}' inválido. Use HTTP/HTTPS absoluta.");
            }

            if (string.IsNullOrWhiteSpace(options.OAuth.ClientId))
            {
                failures.Add("SchemaRegistry:OAuth:ClientId é obrigatório com AuthType=OAuthBearer.");
            }

            if (string.IsNullOrWhiteSpace(options.OAuth.ClientSecret))
            {
                failures.Add("SchemaRegistry:OAuth:ClientSecret é obrigatório com AuthType=OAuthBearer (sempre via Vault/env var).");
            }

            if (options.OAuth.RefreshSkewSeconds < 0)
            {
                failures.Add("SchemaRegistry:OAuth:RefreshSkewSeconds não pode ser negativo.");
            }

            if (options.OAuth.RequestTimeoutMs <= 0)
            {
                failures.Add("SchemaRegistry:OAuth:RequestTimeoutMs deve ser positivo.");
            }
        }

        if (options.RequestTimeoutMs <= 0)
        {
            failures.Add("SchemaRegistry:RequestTimeoutMs deve ser positivo.");
        }

        if (options.MaxCachedSchemas <= 0)
        {
            failures.Add("SchemaRegistry:MaxCachedSchemas deve ser positivo.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
