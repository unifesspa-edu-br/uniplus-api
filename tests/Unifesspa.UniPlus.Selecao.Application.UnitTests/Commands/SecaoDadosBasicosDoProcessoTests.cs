namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>A mescla dos dados básicos só vale para o formulário da inscrição que guarda o conjunto.</summary>
public sealed class SecaoDadosBasicosDoProcessoTests
{
    private readonly ProcessoSeletivo _processo = ProcessoSeletivoConformeBuilder.Criar("PS Inscrição", out _);

    [Fact(DisplayName = "Itens enviados para código que o processo não tem seguem sem mescla com o conjunto básico")]
    public void Mesclar_CodigoQueOProcessoNaoTem_NaoMescla()
    {
        DefinirFatosColetadosCommand pedido = new(_processo.Id, "NAO_EXISTE", []);

        ColetaMesclada mesclada = SecaoDadosBasicosDoProcesso.Mesclar(_processo, CodigoDoFormulario.Criar("NAO_EXISTE").Value!, pedido);

        mesclada.Itens.Should().BeEmpty();
        mesclada.RecusasDaSecao.Should().BeEmpty();
        mesclada.GruposOmitidosRegravados.Should().BeFalse();
    }
}
