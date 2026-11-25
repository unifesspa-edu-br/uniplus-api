namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TermoDeBuscaTests
{
    private const string MensagemDeBuscaMuitoLonga = "O texto pesquisado excede 200 caracteres, sem contar os espaços das pontas.";

    [Theory(DisplayName = "Texto ausente, em branco ou só com caractere nulo não é busca")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0")]
    public void Criar_SemConteudoPesquisavel_DevolveSucessoNulo(string? pedido)
    {
        Result<TermoDeBusca?> resultado = TermoDeBusca.Criar(pedido);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact]
    public void Criar_CaractereNuloNoMeio_DescartaOCaractere() =>
        TermoDeBusca.Criar("a\0b").Value!.Normalizado.Should().Be("ab");

    [Fact]
    public void Criar_CuringasDoLike_PreservaSemEscapar() =>
        TermoDeBusca.Criar("a%b_c\\d").Value!.Normalizado.Should().Be("a%b_c\\d");

    [Fact]
    public void Criar_AcentoECaixa_NormalizaParaMinusculaSemAcento() =>
        TermoDeBusca.Criar("AÇÃO Ñandú").Value!.Normalizado.Should().Be("acao nandu");

    [Fact]
    public void Criar_FormaDecomposta_ProduzOMesmoTermoDaComposta() =>
        TermoDeBusca.Criar("José").Should().BeEquivalentTo(TermoDeBusca.Criar("José"));

    [Fact]
    public void Criar_LetraForaDaTabela_PassaIntacta() =>
        TermoDeBusca.Criar("Ørsted").Value!.Normalizado.Should().Be("ørsted");

    [Fact]
    public void Criar_TextoComEspacosNasPontas_Apara() =>
        TermoDeBusca.Criar("  Processo seletivo  ").Value!.Normalizado.Should().Be("processo seletivo");

    [Fact]
    public void Criar_DuzentosCaracteres_Cabem() =>
        TermoDeBusca.Criar(new string('A', 200)).Value!.Normalizado.Should().Be(new string('a', 200));

    [Fact]
    public void Criar_DuzentosEUmCaracteres_RecusaComOCodigoEAMensagem()
    {
        Result<TermoDeBusca?> resultado = TermoDeBusca.Criar(new string('a', 201));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Consulta.BuscaMuitoLonga");
        resultado.Error.Message.Should().Be(MensagemDeBuscaMuitoLonga);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\u00a0")]
    public void Criar_DuzentosCaracteresComEspacosNasPontas_Aceita(string espaco) =>
        TermoDeBusca.Criar(espaco + new string('a', 200) + espaco).IsSuccess.Should().BeTrue();

    [Fact]
    public void Criar_DuzentosCaracteresComMilharesDeEspacosNasPontas_AceitaEGuardaOTermoAparado()
    {
        string espacos = new(' ', 5000);

        Result<TermoDeBusca?> resultado = TermoDeBusca.Criar(espacos + new string('A', 200) + espacos);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Normalizado.Should().Be(new string('a', 200));
    }

    [Fact]
    public void Criar_DuzentosEUmCaracteresUteisComMilharesDeEspacosNasPontas_RecusaComOCodigoEAMensagem()
    {
        string espacos = new(' ', 5000);

        Result<TermoDeBusca?> resultado = TermoDeBusca.Criar(espacos + new string('a', 201) + espacos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Consulta.BuscaMuitoLonga");
        resultado.Error.Message.Should().Be(MensagemDeBuscaMuitoLonga);
    }

    [Fact]
    public void Criar_ApenasEspacosAcimaDoLimite_DevolveNulo() =>
        TermoDeBusca.Criar(new string(' ', 250)).Value.Should().BeNull();

    [Fact]
    public void Criar_DuzentosEUmCaracteresComEspacoNoMeio_Recusa() =>
        TermoDeBusca.Criar(new string('a', 100) + " " + new string('a', 100)).IsFailure.Should().BeTrue();

    [Fact]
    public void Criar_LetraDecomposta_ContaCadaCaractere() =>
        TermoDeBusca.Criar(new string('a', 199) + "e\u0301").IsFailure.Should().BeTrue();

    [Fact]
    public void Criar_TextoQueSoCaberiaSemOCaractereNulo_RecusaPorqueConfereOComprimentoAntes() =>
        TermoDeBusca.Criar(new string('a', 200) + "\0").Error!.Code.Should().Be("Consulta.BuscaMuitoLonga");

    [Fact]
    public void ToString_NaoContemOTermo() =>
        TermoDeBusca.Criar("Processo seletivo").Value!.ToString().Should().NotContainEquivalentOf("processo");

    [Fact]
    public void ConferirComprimento_TextoQueCabe_NaoDevolveRecusa()
    {
        TermoDeBusca.ConferirComprimento(null).Should().BeNull();
        TermoDeBusca.ConferirComprimento(new string('a', TermoDeBusca.ComprimentoMaximo)).Should().BeNull();
    }
}
