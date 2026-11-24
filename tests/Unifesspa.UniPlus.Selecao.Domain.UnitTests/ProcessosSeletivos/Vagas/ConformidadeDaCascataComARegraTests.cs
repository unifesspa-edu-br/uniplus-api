namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

public sealed class ConformidadeDaCascataComARegraTests
{
    [Theory]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q"]}]}""", true)]
    [InlineData("""{"fallbackCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q"]}]}""", false)]
    public void Conferir_ChaveDoDestinoFinal_ConfereSomenteAChaveAtual(string declaracao, bool esperado)
    {
        RegraCatalogo regra = CascataComRegraBuilder.Regra(declaracao);
        ConfiguracaoCascataRemanejamento cascata = CascataComRegraBuilder.Cascata(regra);

        ConformidadeDaCascataComARegra.Conferir(cascata, regra).Should().Be(esperado);
    }
}
