namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O campo do grupo repetível coleta só fato declarado de membro, e o item comum nunca coleta fato
/// de membro (UNI-REQ-0146).
/// </summary>
public sealed class ConferenciaDoSubitemTests
{
    [Theory]
    [InlineData(OrigemFato.Declarado, EscopoFato.MembroGrupo, "CAMPO_FORMULARIO:PARENTESCO", null)]
    [InlineData(OrigemFato.Declarado, EscopoFato.Candidato, "CAMPO_FORMULARIO:PARENTESCO", ItemFormularioErrorCodes.FatoNaoColetavel)]
    [InlineData(OrigemFato.Calculado, EscopoFato.MembroGrupo, "REGRA_DE_CALCULO:PARENTESCO", ItemFormularioErrorCodes.FatoNaoColetavel)]
    public void FatoDoSubitem_SoAceitaFatoDeclaradoDeMembro(OrigemFato origem, EscopoFato escopo, string binding, string? esperado)
    {
        Dictionary<string, FatoDoCatalogo> catalogo = Catalogo(origem, escopo, binding);

        ConferenciaNoCatalogo.FatoDoSubitem("PARENTESCO", catalogo)?.Code.Should().Be(esperado);
        (ConferenciaNoCatalogo.FatoDoSubitem("PARENTESCO", catalogo) is null).Should().Be(esperado is null);
    }

    [Fact]
    public void FatoDoItem_FatoDeMembro_NaoEhColetavel()
    {
        ConferenciaNoCatalogo.FatoDoItem("PARENTESCO", Catalogo(OrigemFato.Declarado, EscopoFato.MembroGrupo, "CAMPO_FORMULARIO:PARENTESCO"))!.Code
            .Should().Be(ItemFormularioErrorCodes.FatoNaoColetavel);
    }

    private static Dictionary<string, FatoDoCatalogo> Catalogo(OrigemFato origem, EscopoFato escopo, string binding) =>
        new(StringComparer.Ordinal)
        {
            ["PARENTESCO"] = new("PARENTESCO", TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, origem, binding, FonteValoresFato.Catalogo, escopo, Ativo: true, [], ProtecaoAprovada: true),
        };
}
