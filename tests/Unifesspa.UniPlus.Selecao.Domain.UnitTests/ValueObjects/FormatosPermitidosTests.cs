namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="FormatosPermitidos"/> (Story #918) — substitui o campo singular
/// <c>FormatoPermitido?</c> (PR #900): lista de <see cref="FormatoPermitidoEntry"/> OU o
/// token <c>QUALQUER</c>, mutuamente exclusivos, campo agora obrigatório (nunca "ausente").
/// </summary>
public sealed class FormatosPermitidosTests
{
    [Fact(DisplayName = "Lista {PDF,JPEG,PNG} é aceita, sem teto por formato")]
    public void Criar_ListaDeTresFormatos_Aceita()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(
            qualquer: false,
            entradas: [("PDF", null), ("JPEG", null), ("PNG", null)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Qualquer.Should().BeFalse();
        resultado.Value.Lista.Should().HaveCount(3);
        resultado.Value.Lista!.Select(e => e.Formato).Should().BeEquivalentTo(
            [FormatoPermitido.Pdf, FormatoPermitido.Jpeg, FormatoPermitido.Png]);
        resultado.Value.Lista!.Should().AllSatisfy(e => e.TamanhoMaximoBytesMax.Should().BeNull());
    }

    [Fact(DisplayName = "Item {PDF, 5MB} congela o teto por formato")]
    public void Criar_ItemComTetoPorFormato_CongelaTeto()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(
            qualquer: false,
            entradas: [("PDF", 5_000_000)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormatoPermitidoEntry entrada = resultado.Value!.Lista.Should().ContainSingle().Which;
        entrada.Formato.Should().Be(FormatoPermitido.Pdf);
        entrada.TamanhoMaximoBytesMax.Should().Be(5_000_000);
    }

    [Fact(DisplayName = "QUALQUER é aceito, com Lista nula")]
    public void Criar_Qualquer_Aceita()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(qualquer: true, entradas: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Qualquer.Should().BeTrue();
        resultado.Value.Lista.Should().BeNull();
    }

    [Fact(DisplayName = "QUALQUER com formatos específicos é recusado — mutuamente exclusivos")]
    public void Criar_QualquerComFormatosEspecificos_Recusa()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(
            qualquer: true,
            entradas: [("PDF", null)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.QualquerComFormatosEspecificos");
    }

    [Fact(DisplayName = "Nem QUALQUER nem lista (entradas nula) é recusado — campo obrigatório")]
    public void Criar_NemQualquerNemLista_Recusa()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(qualquer: false, entradas: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    [Fact(DisplayName = "Lista vazia (não-QUALQUER) é recusada — campo obrigatório")]
    public void Criar_ListaVazia_Recusa()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(
            qualquer: false,
            entradas: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    [Fact(DisplayName = "Formato fora do domínio conhecido é recusado")]
    public void Criar_FormatoDesconhecido_Recusa()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(
            qualquer: false,
            entradas: [("DOCX", null)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormatoInvalido");
    }

    [Fact(DisplayName = "Formato duplicado na lista é recusado")]
    public void Criar_FormatoDuplicado_Recusa()
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(
            qualquer: false,
            entradas: [("PDF", null), ("PDF", 1_000)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormatoDuplicado");
    }

    [Theory(DisplayName = "Teto por formato zero ou negativo é recusado")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_TetoPorFormatoNaoPositivo_Recusa(int tamanhoMaximoBytesMax)
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(
            qualquer: false,
            entradas: [("PDF", tamanhoMaximoBytesMax)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.TamanhoMaximoBytesMaxInvalido");
    }

    [Fact(DisplayName = "Teto por formato positivo é aceito")]
    public void Criar_TetoPorFormatoPositivo_Aceita() =>
        FormatosPermitidos.Criar(qualquer: false, entradas: [("PDF", 1)]).IsSuccess.Should().BeTrue();
}
