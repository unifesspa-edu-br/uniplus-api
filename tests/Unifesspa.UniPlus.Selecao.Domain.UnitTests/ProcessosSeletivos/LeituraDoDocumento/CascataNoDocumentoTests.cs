namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

/// <summary>O documento não escreve a ordem do destino da cascata: ela é a posição do destino entre os da mesma origem, a partir de 1.</summary>
public sealed class CascataNoDocumentoTests
{
    private static CascataNoDocumento Cascata(params DestinoDaCascataNoDocumento[] destinos) => new(null, "AMPLA", destinos);

    [Fact(DisplayName = "A ordem conta os destinos de cada origem separadamente, a partir de 1, na ordem das posições")]
    public void Ordem_ContaPorOrigem()
    {
        CascataNoDocumento cascata = Cascata(new(0, "COTA_A", "COTA_B"), new(1, "COTA_B", "AMPLA"), new(2, "COTA_A", "AMPLA"));

        cascata.DestinosComOrdem().Select(static par => (par.Destino.PosicaoNoDocumento, par.Ordem)).Should().Equal((0, 1), (1, 1), (2, 2));
    }

    [Fact(DisplayName = "A ordem segue a posição que o destino tinha na lista enviada, mesmo se a lista vier fora de ordem")]
    public void Ordem_SegueAPosicaoNaListaEnviada()
    {
        CascataNoDocumento cascata = Cascata(new(3, "COTA_A", "AMPLA"), new(1, "COTA_A", "COTA_B"));

        cascata.DestinosComOrdem().Select(static par => (par.Destino.PosicaoNoDocumento, par.Ordem)).Should().Equal((1, 1), (3, 2));
    }

    [Fact(DisplayName = "O destino sem origem escrita conta entre os sem origem, sem falhar")]
    public void OrigemAusente_NaoFalha()
    {
        CascataNoDocumento cascata = Cascata(new(0, null, "AMPLA"), new(1, null, "COTA_B"));

        cascata.DestinosComOrdem().Select(static par => par.Ordem).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "A cascata sem destinos não tem ordem para dar")]
    public void SemDestinos_ListaVazia()
    {
        Cascata().DestinosComOrdem().Should().BeEmpty();
    }
}
