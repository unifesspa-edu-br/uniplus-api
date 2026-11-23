namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class FaseComTiposDeDecisaoBuilder
{
    internal static FaseCronograma Reconstituir(FaseCronograma fase, IReadOnlyList<string?> tipos) =>
        FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!, fase.Id, Origem(fase), Cronograma(fase),
            new PublicacoesDaFase([.. fase.Produtos], fase.EmiteParecerIndividual, fase.RegraRecurso, tipos, fase.RegraDeHomologacao),
            [.. fase.BancasRequeridas]);

    private static OrigemCanonicaDaFase Origem(FaseCronograma fase) =>
        new(fase.FaseCanonicaOrigemId, fase.Codigo, fase.OrigemData, fase.AgrupaEtapas,
            fase.PermiteComplementacao, fase.ColetaInscricao, fase.ColetaSolicitacaoIsencao);

    private static DecisoesDeCronogramaDaFase Cronograma(FaseCronograma fase) =>
        new(fase.Ordem, fase.Inicio, fase.Fim, fase.UnidadeResponsavel)
        { NoMesmoMomentoDaAnterior = fase.NoMesmoMomentoDaAnterior };
}
