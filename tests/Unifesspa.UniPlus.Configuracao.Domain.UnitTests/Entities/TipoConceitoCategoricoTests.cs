namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoConceitoCategoricoTests
{
    private static Result<TipoConceito> CriarComValores(params ValorInformado[]? valores) =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Categorica() with { Valores = valores });

    private static ValorInformado Valor(string? codigo, string? rotulo, bool aprova = false) =>
        DefinicoesDeTipoConceito.Valor(codigo, rotulo, aprova);

    [Fact(DisplayName = "Categórico sem a lista de valores é recusado")]
    public void Valores_Ausentes_Recusa() =>
        CriarComValores(null).DeveRecusarComo(TipoConceitoErrorCodes.ValoresCategoricosInsuficientes, "valores");

    [Theory(DisplayName = "Categórico com menos de dois valores é recusado")]
    [InlineData(0)]
    [InlineData(1)]
    public void Valores_MenosDeDois_Recusa(int quantidade) =>
        CriarComValores([.. Enumerable.Range(1, quantidade).Select(i => Valor($"V{i}", $"Valor {i}"))])
            .DeveRecusarComo(TipoConceitoErrorCodes.ValoresCategoricosInsuficientes, "valores");

    [Fact(DisplayName = "Aceita categórico sem nenhum valor de aprovação")]
    public void Valores_SemAprovacao_Aceita() =>
        CriarComValores(Valor("A", "Alfa"), Valor("B", "Beta")).Value!.Valores.Should().OnlyContain(v => !v.ContaComoAprovacao);

    [Theory(DisplayName = "Recusa código de valor ausente, fora do formato ou grande demais")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("deferido")]
    [InlineData("1DEFERIDO")]
    [InlineData("_DEFERIDO")]
    [InlineData("DEFERIDO PARCIAL")]
    [InlineData("DEFERIDO-PARCIAL")]
    [InlineData("DEFERIDO\0")]
    public void Valor_CodigoInvalido_Recusa(string? codigo) =>
        CriarComValores(Valor(codigo, "Rótulo"), Valor("B", "Beta"))
            .DeveRecusarComo(TipoConceitoErrorCodes.ValorCategoricoCodigoInvalido, "valores[0].codigo");

    [Fact(DisplayName = "Aceita código de valor de 50 caracteres e recusa o de 51")]
    public void Valor_CodigoNoLimiteDeTamanho()
    {
        CriarComValores(Valor(new string('A', 50), "Alfa"), Valor("B", "Beta")).IsSuccess.Should().BeTrue();

        CriarComValores(Valor(new string('A', 51), "Alfa"), Valor("B", "Beta"))
            .DeveRecusarComo(TipoConceitoErrorCodes.ValorCategoricoCodigoInvalido, "valores[0].codigo");
    }

    [Fact(DisplayName = "Aceita código de valor com dígitos e sublinhado depois da primeira letra")]
    public void Valor_CodigoComDigitoESublinhado_Aceita() =>
        CriarComValores(Valor("A1_B2", "Alfa"), Valor("B", "Beta")).IsSuccess.Should().BeTrue();

    [Theory(DisplayName = "Recusa rótulo de valor ausente ou com o caractere nulo")]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("Alfa\0")]
    public void Valor_RotuloInvalido_Recusa(string? rotulo) =>
        CriarComValores(Valor("A", "Alfa"), Valor("B", rotulo))
            .DeveRecusarComo(TipoConceitoErrorCodes.ValorCategoricoRotuloInvalido, "valores[1].rotulo");

    [Fact(DisplayName = "Aceita rótulo de valor de 100 caracteres e recusa o de 101")]
    public void Valor_RotuloNoLimiteDeTamanho()
    {
        CriarComValores(Valor("A", new string('R', 100)), Valor("B", "Beta")).IsSuccess.Should().BeTrue();

        CriarComValores(Valor("A", new string('R', 101)), Valor("B", "Beta"))
            .DeveRecusarComo(TipoConceitoErrorCodes.ValorCategoricoRotuloInvalido, "valores[0].rotulo");
    }

    [Fact(DisplayName = "Recusa código de valor repetido, apontando a repetição")]
    public void Valor_CodigoRepetido_Recusa() =>
        CriarComValores(Valor("A", "Alfa"), Valor("B", "Beta"), Valor("A", "Gama"))
            .DeveRecusarComo(TipoConceitoErrorCodes.ValorCategoricoDuplicado, "valores[2].codigo");

    [Fact(DisplayName = "Recusa rótulo de valor repetido, apontando a repetição")]
    public void Valor_RotuloRepetido_Recusa() =>
        CriarComValores(Valor("A", "Alfa"), Valor("B", "Alfa"))
            .DeveRecusarComo(TipoConceitoErrorCodes.ValorCategoricoDuplicado, "valores[1].rotulo");

    [Fact(DisplayName = "Rótulos que só diferem na caixa são distintos")]
    public void Valor_RotulosComCaixaDiferente_SaoDistintos() =>
        CriarComValores(Valor("A", "Alfa"), Valor("B", "ALFA")).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Rótulos iguais em NFC, ainda que em formas Unicode diferentes, são repetidos")]
    public void Valor_RotulosIguaisEmNfc_SaoRepetidos()
    {
        string composto = "Condição";
        string decomposto = composto.Normalize(System.Text.NormalizationForm.FormD);

        CriarComValores(Valor("A", composto), Valor("B", decomposto))
            .DeveRecusarComo(TipoConceitoErrorCodes.ValorCategoricoDuplicado, "valores[1].rotulo");
    }

    [Fact(DisplayName = "Categórico não admite a escala numérica")]
    public void Categorico_ComEscala_Recusa() =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Categorica() with { Numerico = new EscalaInformada(0m, 10m, 2) })
            .DeveRecusarComo(TipoConceitoErrorCodes.ParametrosIncompativeisComANatureza, "numerico");

    [Fact(DisplayName = "Acumula as violações de vários valores no mesmo lote")]
    public void Valores_VariasViolacoes_Acumula()
    {
        Result<TipoConceito> resultado = CriarComValores(Valor("a", ""), Valor("B", "Beta"), Valor("B", "Beta"));

        resultado.Errors.Select(e => e.Field).Should().Equal(
            "valores[0].codigo", "valores[0].rotulo", "valores[2].codigo", "valores[2].rotulo");
    }

    [Fact(DisplayName = "A posição do valor é a do lugar na lista informada")]
    public void Valores_PosicaoSegueALista() =>
        CriarComValores(Valor("C", "Gama"), Valor("A", "Alfa"), Valor("B", "Beta"))
            .Value!.Valores.Select(v => (v.Posicao, v.Codigo)).Should().Equal((1, "C"), (2, "A"), (3, "B"));
}
