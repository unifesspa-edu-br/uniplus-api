namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A normalização do texto do termo e os bytes que compõem o resumo.</summary>
public sealed class ConteudoDoTermoTests
{
    private const string Ato = TermosDeTeste.Ato;

    [Fact(DisplayName = "O resumo é o SHA-256, em hexadecimal minúsculo, dos bytes UTF-8 do texto")]
    public void Criar_ResumoConhecido()
    {
        string texto = "Termo de teste. " + new string('a', 190);

        ConteudoDoTermo conteudo = ConteudoDoTermo.Criar(texto, Ato).Value!;

        conteudo.Resumo.Should().Be("83939c533bf808e0a577e88f9c59faeacbd1057d4e7a737e78dbeb9934f3ab96");
        conteudo.Texto.Should().Be(texto);
    }

    [Fact(DisplayName = "O texto com acento decomposto e o com acento composto têm o mesmo resumo, pela forma NFC")]
    public void Criar_NormalizaNfc()
    {
        string composto = TermosDeTeste.Texto("versão é");
        string decomposto = composto.Replace("é", "é", StringComparison.Ordinal);

        ConteudoDoTermo deComposto = ConteudoDoTermo.Criar(composto, Ato).Value!;
        ConteudoDoTermo deDecomposto = ConteudoDoTermo.Criar(decomposto, Ato).Value!;

        deDecomposto.Resumo.Should().Be(deComposto.Resumo);
        deDecomposto.Texto.Should().Be(composto);
    }

    [Fact(DisplayName = "Quebras de linha viram \\n e somem os espaços no fim das linhas e nas pontas do texto")]
    public void Criar_NormalizaQuebrasEEspacos()
    {
        string texto = TermosDeTeste.Texto();
        string normalizado = $"{texto}\nSegunda linha.\nTerceira linha.";
        string desalinhado = $"  {texto}  \r\nSegunda linha.\t \rTerceira linha.   \n\n";

        ConteudoDoTermo conteudo = ConteudoDoTermo.Criar(desalinhado, Ato).Value!;

        conteudo.Texto.Should().Be(normalizado);
        conteudo.Resumo.Should().Be(ConteudoDoTermo.Resumir(normalizado));
    }

    [Theory(DisplayName = "O texto vazio, curto, longo ou com caractere de controle é recusado")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("curto")]
    [InlineData("longo")]
    [InlineData("nulo")]
    [InlineData("controle")]
    [InlineData("surrogate")]
    public void Criar_TextoInvalido_Recusa(string? caso) =>
        Erros(ConteudoDoTermo.Criar(TextoDoCaso(caso), Ato)).Should().Equal(("texto", TermoErrorCodes.TextoInvalido));

    [Fact(DisplayName = "O texto de 200 caracteres e o de 20 000, depois de normalizados, são aceitos")]
    public void Criar_NosLimites_Aceita()
    {
        ConteudoDoTermo.Criar(new string('a', ConteudoDoTermo.TextoMinLength) + "   ", Ato).IsSuccess.Should().BeTrue();
        ConteudoDoTermo.Criar(new string('a', ConteudoDoTermo.TextoMaxLength), Ato).IsSuccess.Should().BeTrue();
        ConteudoDoTermo.Criar(new string('a', ConteudoDoTermo.TextoMinLength - 1), Ato).IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "O ato vazio ou longo é recusado, e as violações do texto e do ato se acumulam")]
    public void Criar_AtoInvalido_AcumulaComOTexto()
    {
        Erros(ConteudoDoTermo.Criar(TermosDeTeste.Texto(), new string('a', ConteudoDoTermo.AtoMaxLength + 1)))
            .Should().Equal(("ato", TermoErrorCodes.AtoInvalido));
        Erros(ConteudoDoTermo.Criar("curto", "  ")).Should().Equal(("texto", TermoErrorCodes.TextoInvalido), ("ato", TermoErrorCodes.AtoInvalido));
    }

    [Fact(DisplayName = "O ato fica fora do resumo: o mesmo texto com outro ato tem o mesmo resumo")]
    public void Criar_AtoForaDoResumo() =>
        ConteudoDoTermo.Criar(TermosDeTeste.Texto(), "Outro ato").Value!.Resumo
            .Should().Be(TermosDeTeste.Conteudo().Resumo);

    private static string? TextoDoCaso(string? caso) => caso switch
    {
        "curto" => new string('a', ConteudoDoTermo.TextoMinLength - 1),
        "longo" => new string('a', ConteudoDoTermo.TextoMaxLength + 1),
        "nulo" => TermosDeTeste.Texto() + "\0",
        "controle" => TermosDeTeste.Texto() + "\u0007",
        "surrogate" => TermosDeTeste.Texto() + "\ud800",
        _ => caso,
    };

    private static (string? Campo, string Codigo)[] Erros<T>(Result<T> resultado) =>
        [.. resultado.Errors.Select(e => (e.Field, e.Error.Code))];
}
