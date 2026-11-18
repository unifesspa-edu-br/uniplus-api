namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

public sealed class FormaDoCodigoDeErroTests
{
    [Fact(DisplayName = "Todo código de erro do catálogo segue a forma da ADR-0023")]
    public void TodoCodigoDoCatalogoSegueAForma()
    {
        DocumentoDoCatalogoDeErros catalogo = DocumentoDoCatalogoDeErros.Gerar(
            AgregadorDoCatalogoDeErros.Agregar(RegistrosDeErroDaSolucao.Instanciar()));

        string[] foraDaForma = [.. catalogo.Codigos.Select(static e => e.Codigo).Where(static c => !FormaDoCodigoDeErro.EstaNaForma(c))];

        catalogo.Codigos.Should().NotBeEmpty("sem código no catálogo, a conferência passaria vazia");
        foraDaForma.Should().BeEmpty("o código segue ^[a-z]+(\\.[a-z_]+)+$: sem dígito, sem hífen e sem maiúscula");
    }

    [Theory(DisplayName = "A regra aceita códigos na forma da ADR-0023")]
    [InlineData("uniplus.auth.forbidden")]
    [InlineData("uniplus.selecao.processo_seletivo.etapa_invalida")]
    [InlineData("uniplus.paginacao.cursor_invalido")]
    public void AceitaCodigoNaForma(string codigo) =>
        FormaDoCodigoDeErro.EstaNaForma(codigo).Should().BeTrue();

    [Theory(DisplayName = "A regra recusa dígito, hífen, maiúscula, segmento vazio e código sem ponto")]
    [InlineData("uniplus.selecao.processo_seletivo.regra_da_lei_12711")]
    [InlineData("uniplus.paginacao.pagina-invalida")]
    [InlineData("uniplus.selecao.Inscricao.cpf_invalido")]
    [InlineData("uniplus..cpf_invalido")]
    [InlineData("uniplus")]
    [InlineData("1uniplus.auth.forbidden")]
    [InlineData("uniplus.auth.forbidden\n")]
    public void RecusaCodigoForaDaForma(string codigo) =>
        FormaDoCodigoDeErro.EstaNaForma(codigo).Should().BeFalse();
}
