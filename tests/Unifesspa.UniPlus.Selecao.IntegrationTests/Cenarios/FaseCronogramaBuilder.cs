namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Cenarios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class FaseCronogramaBuilder
{
    internal static FaseCronograma Criar()
    {
        Result<FaseCronograma> fase = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), "AVALIACAO", OrigemDataFase.NaExecucao, false, false, false, false),
            new DecisoesDeCronogramaDaFase(1, null, null, UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], false, null), bancasRequeridas: []);
        fase.IsSuccess.Should().BeTrue();
        return fase.Value!;
    }
}
