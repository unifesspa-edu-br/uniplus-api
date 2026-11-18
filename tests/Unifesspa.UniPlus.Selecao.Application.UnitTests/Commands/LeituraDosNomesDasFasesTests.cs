namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class LeituraDosNomesDasFasesTests
{
    [Fact]
    public async Task LerAsync_FasesCitadas_LeUmaVezEEntregaNomesPorOrigem()
    {
        ProcessoSeletivo processo = ProcessosDoPainelDeConformidade.QueDeclaraOTipoUnicoNoCronograma();
        FaseCronograma fase = processo.CronogramaFases.Single();
        IFaseCanonicaReader leitor = Substitute.For<IFaseCanonicaReader>();
        leitor.ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new FaseCanonicaView(fase.FaseCanonicaOrigemId, fase.Codigo, "Classificação", null, false, false, null, false, false, "NA_EXECUCAO")]);
        ServicosDoCongelamentoDeTeste servicos = new();
        ConferenciaDoCatalogoEDaProtecao catalogos = new(servicos.CatalogoDeFatos, servicos.TiposDocumento, leitor);

        CatalogoVivo resultado = await catalogos.LerAsync(processo, CancellationToken.None);

        resultado.NomesDasFases.Should().ContainKey(fase.FaseCanonicaOrigemId).WhoseValue.Should().Be("Classificação");
        await leitor.Received(1).ListarVivasPorCodigosAsync(Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.SequenceEqual(new[] { fase.Codigo })), Arg.Any<CancellationToken>());
    }
}
