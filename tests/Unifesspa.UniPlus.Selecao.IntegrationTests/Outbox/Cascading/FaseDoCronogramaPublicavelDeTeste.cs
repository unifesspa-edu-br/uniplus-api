namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A fase do catálogo que acompanha, no cronograma do processo publicável, a fase de resultado final que o semeador já põe: sem
/// produto, com período no edital e a unidade responsável de teste, como a fase de um edital que só tem datas.
/// </summary>
internal static class FaseDoCronogramaPublicavelDeTeste
{
    /// <summary>A origem do catálogo com o período no edital e nenhuma marca ligada, nem a de chamadas da HABILITACAO; quem precisa de marca usa <c>with</c>.</summary>
    internal static OrigemCanonicaDaFase OrigemNoEdital(string codigo) =>
        OrigensDoTipoDeFaseDeTeste.Da(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false,
            coletaSolicitacaoIsencao: false).ComChamadas(false);

    internal static FaseCronograma Criar(
        OrigemCanonicaDaFase origem, int ordem, DateTimeOffset inicio, DateTimeOffset fim, bool noMesmoMomentoDaAnterior = false) =>
        FaseCronograma.Criar(FormacaoDeChaveLocal.Formar("fase", origem.Codigo, new ChavesEmUso()),
            origem,
            new DecisoesDeCronogramaDaFase(ordem, inicio, fim, UnidadeResponsavel: UnidadesDeTeste.Ceps) { NoMesmoMomentoDaAnterior = noMesmoMomentoDaAnterior },
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    /// <summary>A fase com período de dez dias no mês de 2026 informado.</summary>
    internal static FaseCronograma Criar(string codigo, int ordem, int mes, bool noMesmoMomentoDaAnterior = false) =>
        Criar(
            OrigemNoEdital(codigo), ordem, new DateTimeOffset(2026, mes, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, mes, 10, 0, 0, 0, TimeSpan.Zero),
            noMesmoMomentoDaAnterior);
}
