namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Serialization;

public enum CodigoIndicadorDoProcesso
{
    [JsonStringEnumMemberName("INSCRICOES")]
    Inscricoes,

    [JsonStringEnumMemberName("INSCRITOS")]
    Inscritos,

    [JsonStringEnumMemberName("RASCUNHOS")]
    Rascunhos,
}

public sealed record IndicadorDoProcessoDto(CodigoIndicadorDoProcesso Codigo, long Valor, string Label, string Descricao);

public sealed record IndicadoresDoProcessoLinksDto(string Self, string Processo);

public sealed record IndicadoresDoProcessoDto(
    Guid ProcessoSeletivoId,
    DateTimeOffset ConsultadoEm,
    IReadOnlyList<IndicadorDoProcessoDto> Indicadores,
    [property: JsonPropertyName("_links")] IndicadoresDoProcessoLinksDto Links);
