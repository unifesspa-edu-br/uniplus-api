namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Projeta para o contrato REST a classificação do processo: a configuração de cálculo, eliminação e
/// pesos por área do ENEM, e os critérios de desempate.
/// </summary>
internal static class ProjecaoDaClassificacao
{
    internal static IReadOnlyList<CriterioDesempateDto> ProjetarCriteriosDeDesempate(ProcessoSeletivo processo) =>
        [.. processo.CriteriosDesempate.OrderBy(static c => c.Ordem).Select(ProjetarCriterioDeDesempate)];

    internal static ConfiguracaoClassificacaoDto? Projetar(ProcessoSeletivo processo) =>
        processo.Classificacao is { } classificacao
            ? new(
                classificacao.Id,
                ProjecaoDoProcessoSeletivo.Referencia(classificacao.RegraCalculo),
                classificacao.RegraArredondamento is { } arredondamento ? ProjecaoDoProcessoSeletivo.Referencia(arredondamento) : null,
                classificacao.CasasArredondamento,
                ProjecaoDoProcessoSeletivo.Referencia(classificacao.RegraOrdemAlocacao),
                [.. classificacao.RegrasEliminacao.Select(ProjetarRegraDeEliminacao)],
                Domain.ProcessosSeletivos.Vagas.RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(processo.Declaracao()),
                classificacao.BaseadoEmEnem,
                classificacao.ResolucaoPesoAreaEnem,
                ProjetarQuadroDePesos(classificacao),
                classificacao.EscalaNotaFinal)
            : null;

    private static CriterioDesempateDto ProjetarCriterioDeDesempate(CriterioDesempate criterio) =>
        new(criterio.Id, criterio.Ordem, ProjecaoDoProcessoSeletivo.Referencia(criterio.Regra), ProjetarParametrosDoDesempate(criterio.Parametros));

    private static ParametrosDoCriterioDesempateDto ProjetarParametrosDoDesempate(ParametrosCriterioDesempate parametros) =>
        parametros switch
        {
            ParametrosDesempateMaiorNotaEtapa etapa => new(etapa.EtapaRef, null, null, null, null, null),
            ParametrosDesempateIdoso idoso => new(null, idoso.IdadeMinima, null, null, null, null),
            ParametrosDesempatePredicadoFato predicado => new(
                null, null,
                predicado.Condicao.Fato,
                predicado.Condicao.Operador.Codigo(),
                ProjetarValorDaCondicao(predicado.Condicao.Valor),
                null),
            ParametrosDesempateMaiorNotaAreaEnem areaEnem => new(null, null, null, null, null, [.. areaEnem.Areas]),
            _ => new(null, null, null, null, null, null),
        };

    /// <summary>
    /// <c>CondicaoDnf.Valor</c> como texto JSON canônico, o mesmo formato de
    /// <see cref="CondicaoGatilhoDto.Valor"/>: as duas leituras carregam a mesma tripla
    /// (fato, operador, valor) e o mesmo PUT as recebe de volta.
    /// </summary>
    /// <remarks>
    /// Desachatar a string — devolver <c>PRETA</c> em vez de <c>"PRETA"</c> — perdia o tipo no
    /// ida-e-volta: um valor de texto <c>"18"</c> voltava como <c>18</c> e era reinterpretado
    /// como número na gravação seguinte, mudando qual ramo da matriz operador × domínio o
    /// validaria.
    /// </remarks>
    private static string ProjetarValorDaCondicao(JsonElement valor) => valor.GetRawText();

    private static IReadOnlyList<GrupoPesoAreaEnemCongeladoDto> ProjetarQuadroDePesos(ConfiguracaoClassificacao classificacao) =>
        [.. classificacao.QuadroPesoAreaEnem
            .OrderBy(static g => g.GrupoAreaEnem.Codigo, StringComparer.Ordinal)
            .Select(ProjetarGrupoDePesos)];

    private static GrupoPesoAreaEnemCongeladoDto ProjetarGrupoDePesos(GrupoPesoAreaEnemCongelado g) => new(
        new GrupoAreaEnemSnapshotDto(g.GrupoAreaEnem.Codigo, g.GrupoAreaEnem.Rotulo),
        g.BaseLegal,
        [.. g.Areas
            .OrderBy(static a => a.Codigo, StringComparer.Ordinal)
            .Select(static a => new AreaPesoAreaEnemCongeladaDto(a.Codigo, a.Rotulo, a.Peso, a.Corte))]);

    private static RegraEliminacaoDto ProjetarRegraDeEliminacao(RegraEliminacao regra)
    {
        ReferenciaRegraDto referenciaRegra = ProjecaoDoProcessoSeletivo.Referencia(regra.Regra);

        return regra.Parametros switch
        {
            ParametrosElimNotaMinimaEtapa parametros => new RegraEliminacaoDto(regra.Id, referenciaRegra, parametros.EtapaRef, parametros.NotaMinima, null, null),
            ParametrosElimAusenciaNaEtapa parametros => new RegraEliminacaoDto(regra.Id, referenciaRegra, parametros.EtapaRef, null, null, null),
            ParametrosElimCorteEmArea parametros => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, parametros.Minimo, parametros.AreaCodigo),
            _ => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, null, null),
        };
    }
}
