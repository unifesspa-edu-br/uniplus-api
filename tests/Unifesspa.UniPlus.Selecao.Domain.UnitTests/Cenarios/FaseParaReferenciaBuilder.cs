namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class FaseParaReferenciaBuilder
{
    internal static FaseCronograma Criar(bool coleta, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(),
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital, false, false, coleta, false),
            new DecisoesDeCronogramaDaFase(1, inicio, fim, null),
            new PublicacoesDaFase([], false, null), []);
}
