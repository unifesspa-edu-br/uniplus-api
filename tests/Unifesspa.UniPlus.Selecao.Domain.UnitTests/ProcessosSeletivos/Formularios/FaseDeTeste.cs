namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class FaseDeTeste
{
    internal static FaseCronograma Criar(
        int ordem,
        string codigo,
        bool coletaInscricao = false,
        bool coletaSolicitacaoIsencao = false,
        bool comDatas = true,
        bool noMesmoMomentoDaAnterior = false) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, comDatas ? OrigemDataFase.NoEdital : OrigemDataFase.NaExecucao, AgrupaEtapas: false,
                PermiteComplementacao: false, coletaInscricao, coletaSolicitacaoIsencao)),
            new DecisoesDeCronogramaDaFase(
                ordem, comDatas ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : null,
                comDatas ? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero) : null,
                UnidadesDeTeste.Ceps)
            { NoMesmoMomentoDaAnterior = noMesmoMomentoDaAnterior },
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []).Value!;
}
