namespace Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuração do cliente do Geo (issue #1865) — ao contrário do SIGAA (integração
/// opcional, condicionada a <c>BaseUrl</c> configurado), esta é OBRIGATÓRIA: a conferência do
/// endereço do candidato é invariante de elegibilidade (ADR-0136), nunca um "melhor esforço".
/// <c>[Required]</c> + <see cref="GeoOptionsServiceCollectionExtensions.AddGeoClient"/> chamando
/// <c>ValidateOnStart()</c> incondicionalmente fazem o processo não subir sem esta config — mesmo
/// padrão de <c>EncryptionOptions</c>.
/// </summary>
public sealed class GeoOptions
{
    public const string SectionName = "Geo";

    /// <summary>Endereço base da API do Geo (ex.: <c>http://geo-api:8080</c> no compose, sem barra final).</summary>
    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Tempo limite total de uma chamada ao Geo, incluindo retentativas.</summary>
    public int TimeoutTotalEmSegundos { get; init; } = 10;

    /// <summary>Tempo limite de cada tentativa individual.</summary>
    public int TimeoutPorTentativaEmSegundos { get; init; } = 4;

    public int MaximoDeRetentativas { get; init; } = 2;

    public GeoOAuthOptions OAuth { get; init; } = new();
}

/// <summary>
/// Credenciais <c>client_credentials</c> contra o Keycloak — o MESMO client M2M já provisionado
/// para o Schema Registry (<c>uniplus-api-selecao</c>, issue uniplus-infra#163): o Geo exige só um
/// Bearer de qualquer aplicação UNIFESSPA do mesmo realm, sem restrição de audiência, então não há
/// necessidade de um client novo — só de uma seção de config própria, para não acoplar o cliente do
/// Geo à configuração do Schema Registry (módulos diferentes, mesma identidade operacional).
/// </summary>
/// <remarks>
/// Deliberadamente SEM <c>[Required]</c>: diferente de <see cref="GeoOptions.BaseUrl"/>,
/// ausência aqui não derruba o startup do monólito inteiro (o secret vem de Vault/env var em todo
/// ambiente real, nunca committed) — vira falha fechada só QUANDO alguém de fato chama o Geo
/// (<c>GeoTokenProvider.AutenticarAsync</c> falha, <c>ConferenciaEnderecoGeo</c> traduz em
/// <see cref="Application.Abstractions.ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel"/>), não
/// quando qualquer teste de integração não relacionado ao Geo sobe o host.
/// </remarks>
public sealed class GeoOAuthOptions
{
    public string TokenEndpoint { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string? Scope { get; init; }

    /// <summary>Margem de segurança antes do vencimento declarado (<c>expires_in</c>) para renovar proativamente.</summary>
    public int MargemDeRenovacaoEmSegundos { get; init; } = 30;
}
