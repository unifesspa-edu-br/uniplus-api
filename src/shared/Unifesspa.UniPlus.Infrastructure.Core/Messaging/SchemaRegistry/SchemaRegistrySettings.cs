namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Bound options para o cliente Confluent Schema Registry (Apicurio em modo Confluent-compat).
/// Mapeia a seção <c>SchemaRegistry:</c> de
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Modos de autenticação suportados:
/// </para>
/// <list type="bullet">
///   <item><description><b>None</b> (Development / docker-compose sem auth): apenas <see cref="Url"/>.</description></item>
///   <item><description><b>Basic</b> (Apicurio com basic auth, dev/lab simples):
///     <see cref="Url"/> + <see cref="AuthType"/>=<c>Basic</c> + <see cref="BasicAuthUserInfo"/>=<c>user:password</c>.</description></item>
///   <item><description><b>OAuthBearer</b> (standalone/HML/Prod com Apicurio OIDC, ADR-0051):
///     <see cref="Url"/> + <see cref="AuthType"/>=<c>OAuthBearer</c> + <see cref="OAuth"/> populado
///     (token endpoint do Keycloak <c>/protocol/openid-connect/token</c>,
///     <c>uniplus-api-{portal,selecao,ingresso}</c> client + secret no Vault).</description></item>
/// </list>
/// <para>
/// Quando <see cref="Url"/> está vazio, o cliente Schema Registry e o hosted service
/// de registro inicial não são registrados — comportamento de "feature off" útil em
/// dev local quando o Apicurio não está rodando.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "Bound options via IConfiguration — System.Text.Json não desserializa Uri nativamente. Validação de formato em SchemaRegistrySettingsValidator.")]
public sealed class SchemaRegistrySettings
{
    public const string SectionName = "SchemaRegistry";

    /// <summary>
    /// URL base do Schema Registry. Para Apicurio em modo Confluent-compat,
    /// inclui o sufixo <c>/apis/ccompat/v7</c>
    /// (ex.: <c>https://schema-registry.standalone.portaluni.com.br/apis/ccompat/v7</c>).
    /// Vazio desliga a feature.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Tipo de autenticação. Valores aceitos (case-insensitive):
    /// <c>None</c>, <c>Basic</c>, <c>OAuthBearer</c>. Default <c>None</c>.
    /// </summary>
    public string AuthType { get; init; } = "None";

    /// <summary>
    /// Credenciais Basic no formato <c>user:password</c>. Obrigatório quando
    /// <see cref="AuthType"/>=<c>Basic</c>. Sempre via env var/Vault — nunca em arquivo Git.
    /// </summary>
    public string? BasicAuthUserInfo { get; init; }

    /// <summary>
    /// Configuração OAuth client_credentials. Obrigatória quando
    /// <see cref="AuthType"/>=<c>OAuthBearer</c>.
    /// </summary>
    public OAuthBearerSettings OAuth { get; init; } = new();

    /// <summary>
    /// Timeout HTTP por request ao Schema Registry (default 30s). Apicurio standalone
    /// pode demorar em primeira chamada (carrega cache + valida JWT contra Keycloak).
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 30_000;

    /// <summary>
    /// Tamanho do cache local de schemas no <c>CachedSchemaRegistryClient</c>
    /// (default 1000). Cobre todos os subjects do Uni+ com folga.
    /// </summary>
    public int MaxCachedSchemas { get; init; } = 1000;
}

/// <summary>
/// Configuração OAuth client_credentials para autenticação contra o Apicurio Registry.
/// </summary>
/// <remarks>
/// O cliente cacheia o bearer JWT obtido do <see cref="TokenEndpoint"/> e o renova
/// proativamente antes da expiração (skew configurável). Detalhes em
/// <c>OAuthBearerAuthenticationHeaderValueProvider</c>.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "Bound options via IConfiguration — String para token endpoint padrão entre options do Uni+. Validação em SchemaRegistrySettingsValidator.")]
public sealed class OAuthBearerSettings
{
    /// <summary>
    /// Endpoint <c>/protocol/openid-connect/token</c> do Keycloak realm <c>uniplus</c>
    /// (ex.: <c>https://standalone.portaluni.com.br/auth/realms/uniplus/protocol/openid-connect/token</c>).
    /// </summary>
    public string TokenEndpoint { get; init; } = string.Empty;

    /// <summary>
    /// <c>client_id</c> do confidential client M2M no realm. Para uniplus-api,
    /// um de <c>uniplus-api-portal</c>, <c>uniplus-api-selecao</c>, <c>uniplus-api-ingresso</c>
    /// (issue uniplus-infra#163).
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// <c>client_secret</c> custodiado em Vault em
    /// <c>secret/standalone/keycloak/clients/uniplus-api-{portal,selecao,ingresso}</c>
    /// (RUNBOOKS §15.6 do uniplus-infra) e injetado via env var
    /// <c>SchemaRegistry__OAuth__ClientSecret</c> pelo ESO 5 dos charts API.
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Escopo OAuth opcional (ex.: <c>openid</c>). Default vazio — Keycloak emite
    /// access_token sem scope explícito quando o request omite o parâmetro.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Margem de segurança em segundos antes do <c>exp</c> do JWT em que o cliente
    /// renova proativamente (default 30s). Evita 401 em chamadas concorrentes que
    /// pegam um token quase-expirado.
    /// </summary>
    public int RefreshSkewSeconds { get; init; } = 30;

    /// <summary>
    /// Timeout HTTP do request ao token endpoint (default 10s).
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 10_000;
}
