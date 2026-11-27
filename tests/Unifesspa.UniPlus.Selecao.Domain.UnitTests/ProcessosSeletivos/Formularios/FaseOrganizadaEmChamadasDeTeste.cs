namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class FaseOrganizadaEmChamadasDeTeste
{
    internal static FaseCronograma Criar(int ordem, string codigo, string chave) =>
        FaseCronograma.Criar(ChaveLocal.Criar(chave).Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false,
                permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false).ComChamadas(true),
            new DecisoesDeCronogramaDaFase(
                ordem, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []).Value!;
}
