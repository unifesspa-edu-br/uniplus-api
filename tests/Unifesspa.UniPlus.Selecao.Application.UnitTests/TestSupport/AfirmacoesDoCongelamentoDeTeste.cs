namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>O que as portas de escrita do congelamento viram, ou não viram, numa chamada.</summary>
internal static class AfirmacoesDoCongelamentoDeTeste
{
    /// <summary>Nada foi canonicalizado, e nem a versão, nem o ato, nem a remoção do rascunho, nem o flush aconteceram.</summary>
    public static async Task NadaFoiGravadoAsync(this ServicosDoCongelamentoDeTeste servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        _ = servicos.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        await servicos.Repositorio.DidNotReceive().AdicionarVersaoConfiguracaoAsync(Arg.Any<VersaoConfiguracao>(), Arg.Any<CancellationToken>());
        await servicos.Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await servicos.Rascunhos.DidNotReceive().RemoverDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await servicos.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
