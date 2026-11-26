namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal static class RotuloDaRegra
{
    internal static string? Obter(RegraCatalogo regra) => regra.Tipo switch
    {
        TipoRegra.RegraCalculo => Obter<RegraDeCalculoDaNota>(regra.Codigo),
        TipoRegra.RegraArredondamento => Obter<RegraDeArredondamentoDaNota>(regra.Codigo),
        TipoRegra.RegraEliminacao => Obter<RegraDeEliminacaoPorCalculo>(regra.Codigo),
        TipoRegra.RegraFada => Obter<RegraDoFada>(regra.Codigo),
        TipoRegra.CriterioDesempate => Obter<RegraDeDesempate>(regra.Codigo),
        TipoRegra.CriterioRemanejamento => Obter<CriterioDeRemanejamento>(regra.Codigo),
        TipoRegra.RegraOrdemAlocacao => Obter<RegraDeOrdemDeAlocacao>(regra.Codigo),
        TipoRegra.RegraAjusteDistribuicaoVagas => Obter<RegraDeAjusteDaDistribuicaoDeVagas>(regra.Codigo),
        _ => ObterDaAvaliacaoOuDoRecurso(regra),
    };

    private static string? ObterDaAvaliacaoOuDoRecurso(RegraCatalogo regra) => regra.Tipo switch
    {
        TipoRegra.RegraElegibilidade => regra.Codigo == "RENDA-PER-CAPITA-LEI-12711" ? "Renda familiar por pessoa pela Lei 12.711" : null,
        TipoRegra.RegraDistribuicaoVagas => Obter<RegraDeDistribuicaoDeVagas>(regra.Codigo),
        TipoRegra.RegraProcedimentoRecurso => Obter<RegraDeProcedimentoDoRecurso>(regra.Codigo),
        TipoRegra.RegraPrazoRecurso => Obter<RegraDePrazoDoRecurso>(regra.Codigo),
        TipoRegra.ConvencaoContagemPrazo => Obter<ConvencaoDeContagemDoPrazo>(regra.Codigo),
        TipoRegra.RegraApuracaoAvaliacao => Obter<RegraDeApuracaoDaAvaliacao>(regra.Codigo),
        _ => null,
    };

    private static string? Obter<T>(string codigo)
        where T : struct, Enum => CatalogoDe<T>.TentarObter(codigo, out T valor) ? valor.Rotulo() : null;
}
