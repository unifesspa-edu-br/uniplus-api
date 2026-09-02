namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Envelope de coleção no formato Hydra que a API do SIGAA usa em todas as listagens.
/// </summary>
/// <typeparam name="T">Tipo de cada item da coleção.</typeparam>
public sealed record ColecaoHydra<T>
{
    /// <summary>
    /// Itens da página corrente. Página vazia é resposta legítima — significa que o
    /// filtro não casou com nada, não que houve erro.
    /// </summary>
    [JsonPropertyName("hydra:member")]
    public IReadOnlyList<T> Itens { get; init; } = [];

    /// <summary>
    /// Total de itens que o filtro alcança, somando todas as páginas. É a única forma de
    /// saber quantas páginas existem, já que a origem não expõe contagem de páginas.
    /// </summary>
    [JsonPropertyName("hydra:totalItems")]
    public int? TotalDeItens { get; init; }

    /// <summary>
    /// Navegação da paginação. Traz os endereços da primeira, da última e das páginas
    /// vizinhas quando a coleção não cabe numa página só.
    /// </summary>
    [JsonPropertyName("hydra:view")]
    public VisaoHydra? Visao { get; init; }
}

/// <summary>
/// Recorte de navegação de uma coleção paginada.
/// </summary>
public sealed record VisaoHydra
{
    [JsonPropertyName("hydra:first")]
    public string? Primeira { get; init; }

    [JsonPropertyName("hydra:last")]
    public string? Ultima { get; init; }

    [JsonPropertyName("hydra:previous")]
    public string? Anterior { get; init; }

    [JsonPropertyName("hydra:next")]
    public string? Proxima { get; init; }
}
