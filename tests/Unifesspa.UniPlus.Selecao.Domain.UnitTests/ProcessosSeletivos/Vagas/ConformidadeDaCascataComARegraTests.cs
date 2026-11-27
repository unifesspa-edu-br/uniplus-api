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

        ConformidadeDaCascataComARegra.Conferir(cascata, regra.DeclaracaoDaRegra).Should().Be(esperado);
    }

    [Theory(DisplayName = "A declaração fora do formato esperado não confere com a cascata, sem lançar exceção")]
    [InlineData("""{"destinoFinalCodigo":1,"ordens":[]}""")]
    [InlineData("""{"destinoFinalCodigo":null,"ordens":[]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":"LB_PPI"}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":["LB_PPI"]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[1]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[{"origem":1,"destinos":["LB_Q"]}]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[{"origem":null,"destinos":["LB_Q"]}]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":"LB_Q"}]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":[1]}]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":[null]}]}""")]
    [InlineData("""{"destinoFinalCodigo":"AC","ordens":[{"origem":"LB_PPI"}]}""")]
    public void Conferir_DeclaracaoMalformada_NaoConfereSemLancar(string declaracao)
    {
        RegraCatalogo regra = CascataComRegraBuilder.Regra(declaracao);
        ConfiguracaoCascataRemanejamento cascata = CascataComRegraBuilder.Cascata(regra);

        bool conferiu = true;
        Action conferir = () => conferiu = ConformidadeDaCascataComARegra.Conferir(cascata, regra.DeclaracaoDaRegra);

        conferir.Should().NotThrow();
        conferiu.Should().BeFalse();
    }
}
