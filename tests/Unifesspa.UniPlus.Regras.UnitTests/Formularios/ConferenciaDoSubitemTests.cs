namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O campo do grupo repetível coleta só fato declarado de membro, e o item comum nunca coleta fato
/// de membro (UNI-REQ-0146).
/// </summary>
public sealed class ConferenciaDoSubitemTests
{
    [Theory]
    [InlineData("DECLARADO", "MEMBRO_GRUPO", "CAMPO_INSCRICAO:PARENTESCO", null)]
    [InlineData("DECLARADO", "CANDIDATO", "CAMPO_INSCRICAO:PARENTESCO", ItemFormularioErrorCodes.FatoNaoColetavel)]
    [InlineData("DERIVADO", "MEMBRO_GRUPO", "REGRA_DERIVACAO:PARENTESCO", ItemFormularioErrorCodes.FatoNaoColetavel)]
    public void FatoDoSubitem_SoAceitaFatoDeclaradoDeMembro(string origem, string escopo, string binding, string? esperado)
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(origem, escopo, binding);

        ConferenciaNoCatalogo.FatoDoSubitem("PARENTESCO", catalogo)?.Code.Should().Be(esperado);
        (ConferenciaNoCatalogo.FatoDoSubitem("PARENTESCO", catalogo) is null).Should().Be(esperado is null);
    }

    [Fact]
    public void FatoDoItem_FatoDeMembro_NaoEhColetavel()
    {
        ConferenciaNoCatalogo.FatoDoItem("PARENTESCO", Catalogo("DECLARADO", "MEMBRO_GRUPO", "CAMPO_INSCRICAO:PARENTESCO"))!.Code
            .Should().Be(ItemFormularioErrorCodes.FatoNaoColetavel);
    }

    private static Dictionary<string, FatoDoCatalogo> Catalogo(string origem, string escopo, string binding) =>
        new(StringComparer.Ordinal)
        {
            ["PARENTESCO"] = new("PARENTESCO", "CATEGORICO", "ESCALAR", origem, binding, "GLOBAL", escopo, Ativo: true, []),
        };
}
