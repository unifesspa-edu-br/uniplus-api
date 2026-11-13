namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum CodigoIndicadorDoProcesso
{
    [CodigoDeContrato("INSCRICOES", "Inscrições", Descricao = "Submetidas e não canceladas")]
    Inscricoes,

    [CodigoDeContrato("INSCRITOS", "Inscritos", Descricao = "Pessoas distintas com inscrição submetida e não cancelada")]
    Inscritos,

    [CodigoDeContrato("RASCUNHOS", "Rascunhos")]
    Rascunhos,
}

public sealed record IndicadorDoProcessoDto([property: AceitaCodigosDe<CodigoIndicadorDoProcesso>] string Codigo, long Valor, string Rotulo, string Descricao);

public sealed record IndicadoresDoProcessoLinksDto(string Self, string Processo);

public sealed record IndicadoresDoProcessoDto(
    Guid ProcessoSeletivoId,
    DateTimeOffset ConsultadoEm,
    IReadOnlyList<IndicadorDoProcessoDto> Indicadores,
    [property: JsonPropertyName("_links")] IndicadoresDoProcessoLinksDto Links);
