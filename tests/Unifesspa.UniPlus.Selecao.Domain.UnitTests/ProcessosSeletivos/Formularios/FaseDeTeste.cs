namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class FaseDeTeste
{
    internal static FaseCronograma Criar(int ordem, string codigo, bool coletaInscricao = false, bool coletaSolicitacaoIsencao = false, bool comDatas = true) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, comDatas ? OrigemDataFase.NoEdital : OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao, coletaSolicitacaoIsencao,
            inicio: comDatas ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : null, fim: comDatas ? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero) : null, produtos: [],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
}
