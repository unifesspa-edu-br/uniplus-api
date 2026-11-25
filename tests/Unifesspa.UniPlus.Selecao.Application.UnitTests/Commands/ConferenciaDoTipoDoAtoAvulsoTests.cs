namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

public sealed class ConferenciaDoTipoDoAtoAvulsoTests
{
    [Fact(DisplayName = "Código fora do formato sem versão vigente é recusado pelo catálogo")]
    public async Task Conferir_TipoForaDoFormatoSemVersaoVigente_Recusa()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        DateOnly diaDaPublicacao = new(2026, 12, 10);
        ConferenciaDoTipoDoAtoAvulso conferencia = new(new ConferenciaDoTipoDeAto(catalogo));

        Result<TipoAtoPublicadoView> resultado = await conferencia.ConferirAsync("aviso", diaDaPublicacao, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
        await catalogo.Received().ObterVigenteAsync("aviso", diaDaPublicacao, CancellationToken.None);
    }
}
