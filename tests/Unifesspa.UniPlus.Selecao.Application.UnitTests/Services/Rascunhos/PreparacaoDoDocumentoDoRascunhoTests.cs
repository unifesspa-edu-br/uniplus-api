namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class PreparacaoDoDocumentoDoRascunhoTests
{
    private readonly CenarioDoRascunho _cenario = new();

    [Fact(DisplayName = "O documento válido devolve o documento normalizado, o nome e o código da unidade")]
    public void DocumentoValido_DevolveONomeEOCodigoDaUnidade()
    {
        Result<DocumentoPreparado> resultado = _cenario.Preparacao.Preparar(CenarioDoRascunho.DocumentoValido());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CodigoDaUnidade.Should().Be("CEPS");
        resultado.Value.Documento.Nome.Should().Be("Processo Seletivo de Exemplo");
    }

    [Fact(DisplayName = "O nome acima do limite do domínio recusa no ponteiro do nome, com o grupo da forma na frente")]
    public void NomeAcimaDoLimite_RecusaNoPonteiroDoNome()
    {
        string nome = new('n', 301);

        Result<DocumentoPreparado> resultado = _cenario.Preparacao.Preparar(CenarioDoRascunho.DocumentoValido(nome: nome));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.FormaInvalida),
            ("/identificacao/nome", RascunhoDeProcessoSeletivoErrorCodes.NomeAcimaDoLimite));
    }

    [Fact(DisplayName = "A unidade só em branco é unidade ausente")]
    public void UnidadeEmBranco_Ausente()
    {
        Result<DocumentoPreparado> resultado = _cenario.Preparacao.Preparar(CenarioDoRascunho.DocumentoValido("  "));

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente);
    }

    [Fact(DisplayName = "A forma inválida vence a unidade ausente: o documento sem unidade e malformado recusa pela forma")]
    public void FormaVenceUnidadeAusente()
    {
        Result<DocumentoPreparado> resultado = _cenario.Preparacao.Preparar("[1, 2]");

        resultado.Errors[0].Error.Code.Should().Be(DocumentoDaConfiguracaoErrorCodes.FormaInvalida);
    }

    private (string? Ponteiro, string Codigo)[] Recusas(string texto) =>
        [.. _cenario.Preparacao.Preparar(texto).Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "O caractere nulo no valor é recusado no ponteiro do valor, com o grupo da forma na frente")]
    public void CaractereNuloNoValor_RecusaNoPonteiroDoValor() =>
        Recusas(CenarioDoRascunho.DocumentoValido(nome: "Processo\\u0000")).Should().Equal(
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.FormaInvalida),
            ("/identificacao/nome", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Fact(DisplayName = "O caractere nulo no nome de propriedade é recusado e acumula com a propriedade repetida")]
    public void CaractereNuloNoNome_AcumulaComAsOutrasCausas() =>
        Recusas("""{ "a\u0000": 1, "b": 1, "b": 2 }""").Should().Equal(
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.FormaInvalida),
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida));

    [Fact(DisplayName = "Os caracteres não ASCII são guardados como estão, e o que passaria de 2 MiB só pela expansão em escapes é aceito")]
    public void NaoAscii_NaoExpandeNemEstouraOTamanho()
    {
        string texto = "{ \"nota\": \"" + new string('é', 700_000) + "\", " + CenarioDoRascunho.DocumentoValido(nome: "Seleção")[2..];

        Result<DocumentoPreparado> resultado = _cenario.Preparacao.Preparar(texto);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Documento.Conteudo.Should().Contain("Seleção").And.NotContain("\\u");
    }

    [Fact(DisplayName = "O texto nulo é recusado")]
    public void TextoNulo_Lanca() => Assert.Throws<ArgumentNullException>(() => _cenario.Preparacao.Preparar(null!));
}
