namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using System.Text.Json.Serialization;

/// <summary>
/// Situação acadêmica do vínculo, espelhada do vocabulário do SIGAA. O consumidor a
/// reflete como veio — não traduz nem reinterpreta.
/// </summary>
/// <remarks>
/// A origem restringe o que emite aqui aos status que denotam vínculo real: rascunho
/// cadastral e inconsistência ficam de fora antes de a linha chegar ao consumidor.
/// </remarks>
public sealed record SituacaoPayload
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("descricao")]
    public string? Descricao { get; init; }

    /// <summary>
    /// Qualificador de vínculo que a origem associa à situação, quando existe. É campo do
    /// vocabulário do SIGAA, não a situação do vínculo em si.
    /// </summary>
    [JsonPropertyName("situacaoVinculo")]
    public string? SituacaoVinculo { get; init; }
}
