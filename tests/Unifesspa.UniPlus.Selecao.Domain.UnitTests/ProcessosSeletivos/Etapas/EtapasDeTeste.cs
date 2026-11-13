namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class EtapasDeTeste
{
    internal static EtapaProcesso Etapa(string nome, int? ordem = null, bool notaDoEnem = false, bool componeNota = true)
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), notaDoEnem ? "NOTA_ENEM" : "PROVA_OBJETIVA", "Tipo de teste", admitePontuacao: componeNota || notaDoEnem, admiteEliminacao: true, notaDeOrigemNoEnem: notaDoEnem).Value!;
        return componeNota
            ? EtapaProcesso.Reidratar(Guid.CreateVersion7(), nome, CaraterEtapa.Classificatoria, tipo, 1m, null, ordem)
            : EtapaProcesso.Reidratar(Guid.CreateVersion7(), nome, CaraterEtapa.Eliminatoria, tipo, null, null, ordem);
    }

    internal static ConfiguracaoClassificacao ClassificacaoSemEnem(params RegraEliminacao[] eliminacoes) =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            eliminacoes,
            false,
            null,
            [],
            escalaNotaFinal: 100m).Value!;

    internal static RegraEliminacao Eliminacao(string codigo, ArgsRegraEliminacao args) =>
        RegraEliminacao.Criar(Regra(codigo, 'd'), args).Value!;

    internal static CriterioDesempate DesempatePelaNotaDa(EtapaProcesso etapa, int ordem) =>
        CriterioDesempate.Criar(
            ordem, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'e'), new ArgsDesempateMaiorNotaEtapa(etapa.Id)).Value!;

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;
}
