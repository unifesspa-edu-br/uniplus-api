namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// A declaração da importação do resultado (UNI-REQ-0144): os relatórios que o processo importa, cada um pelo
/// código e pela versão do leiaute, sem repetição, e a primeira fase depois da importação. A existência do
/// leiaute é do catálogo, e não da declaração.
/// </summary>
public sealed class DeclaracaoDaImportacaoDoResultadoTests
{
    private static readonly Guid PrimeiraFase = Guid.CreateVersion7();

    private static RelatorioInformado Relatorio(string? codigo, string? versao = "v1") => new(codigo, versao);

    [Fact(DisplayName = "Os relatórios com código e versão são declarados, na ordem em que vêm")]
    public void RelatoriosComLeiaute_SaoDeclarados()
    {
        Result<DeclaracaoDaImportacaoDoResultado> resultado = DeclaracaoDaImportacaoDoResultado.Declarar(
            [Relatorio("CHAMADA_REGULAR"), Relatorio("LISTA_DE_ESPERA", "v2")], PrimeiraFase);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Relatorios.Should().Equal(new RelatorioDeclarado("CHAMADA_REGULAR", "v1"), new RelatorioDeclarado("LISTA_DE_ESPERA", "v2"));
        resultado.Value.PrimeiraFaseDepoisDaImportacaoId.Should().Be(PrimeiraFase);
    }

    [Fact(DisplayName = "O rascunho sem relatório e sem primeira fase ainda é uma declaração: a publicação é que o recusa")]
    public void SemRelatorioESemPrimeiraFase_EDeclaracao()
    {
        Result<DeclaracaoDaImportacaoDoResultado> resultado = DeclaracaoDaImportacaoDoResultado.Declarar([], null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Relatorios.Should().BeEmpty();
        resultado.Value.PrimeiraFaseDepoisDaImportacaoId.Should().BeNull();
    }

    [Theory(DisplayName = "Relatório sem código ou sem versão de leiaute é recusado no campo que falta")]
    [InlineData(null, "v1", "importacao.relatorios[0].leiauteCodigo")]
    [InlineData("  ", "v1", "importacao.relatorios[0].leiauteCodigo")]
    [InlineData("CHAMADA_REGULAR", null, "importacao.relatorios[0].leiauteVersao")]
    [InlineData("CHAMADA_REGULAR", " ", "importacao.relatorios[0].leiauteVersao")]
    public void RelatorioSemLeiaute_Recusa(string? codigo, string? versao, string campo)
    {
        Result<DeclaracaoDaImportacaoDoResultado> resultado = DeclaracaoDaImportacaoDoResultado.Declarar([Relatorio(codigo, versao)], PrimeiraFase);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = campo,
            Error = new { Code = ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute },
        });
    }

    [Fact(DisplayName = "O leiaute repetido é recusado no segundo relatório, mesmo com outra versão")]
    public void LeiauteRepetido_Recusa()
    {
        Result<DeclaracaoDaImportacaoDoResultado> resultado = DeclaracaoDaImportacaoDoResultado.Declarar(
            [Relatorio("CHAMADA_REGULAR"), Relatorio("LISTA_DE_ESPERA"), Relatorio("CHAMADA_REGULAR", "v2")], null);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "importacao.relatorios[2].leiauteCodigo",
            Error = new { Code = ImportacaoDoResultadoErrorCodes.RelatorioRepetido },
        });
    }

    [Fact(DisplayName = "As recusas de relatórios diferentes se acumulam")]
    public void RecusasDeRelatoriosDiferentes_SeAcumulam()
    {
        Result<DeclaracaoDaImportacaoDoResultado> resultado = DeclaracaoDaImportacaoDoResultado.Declarar(
            [Relatorio(null), Relatorio("CHAMADA_REGULAR", null)], null);

        resultado.Errors.Select(static erro => ((FieldError)erro).Field).Should()
            .Equal("importacao.relatorios[0].leiauteCodigo", "importacao.relatorios[1].leiauteVersao");
    }

    [Fact(DisplayName = "As declarações são iguais quando têm os mesmos relatórios, na mesma ordem, e a mesma primeira fase")]
    public void Igualdade_PorValor()
    {
        DeclaracaoDaImportacaoDoResultado a = LeiauteDeTeste.Declarar(PrimeiraFase, "A", "B");

        a.Should().Be(LeiauteDeTeste.Declarar(PrimeiraFase, "A", "B"));
        a.Should().NotBe(LeiauteDeTeste.Declarar(PrimeiraFase, "B", "A"));
        a.Should().NotBe(LeiauteDeTeste.Declarar(null, "A", "B"));
    }
}
