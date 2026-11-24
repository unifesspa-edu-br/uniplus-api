namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>A regra de que SEMPRE e NUNCA não têm predicado e QUANDO sempre tem, num lugar só.</summary>
public sealed class ObrigatoriedadeDaFormaTests
{
    private static readonly PredicadoDnf Condicao = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("IDADE", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!)]).Value!;

    [Fact(DisplayName = "SEMPRE sem predicado é a obrigatoriedade sempre")]
    public void DaForma_SempreSemPredicado_EOSempre() =>
        Obrigatoriedade.DaForma(TipoObrigatoriedade.Sempre, null).Should().BeSameAs(Obrigatoriedade.Sempre);

    [Fact(DisplayName = "NUNCA sem predicado é a obrigatoriedade nunca")]
    public void DaForma_NuncaSemPredicado_EONunca() =>
        Obrigatoriedade.DaForma(TipoObrigatoriedade.Nunca, null).Should().BeSameAs(Obrigatoriedade.Nunca);

    [Fact(DisplayName = "QUANDO com predicado é a obrigatoriedade condicional, com o predicado")]
    public void DaForma_QuandoComPredicado_ELevaOPredicado() =>
        Obrigatoriedade.DaForma(TipoObrigatoriedade.Quando, Condicao)!.Predicado.Should().BeSameAs(Condicao);

    [Theory(DisplayName = "Forma incoerente com o predicado não é obrigatoriedade")]
    [InlineData(TipoObrigatoriedade.Sempre, true)]
    [InlineData(TipoObrigatoriedade.Nunca, true)]
    [InlineData(TipoObrigatoriedade.Quando, false)]
    public void DaForma_FormaIncoerente_DevolveNulo(TipoObrigatoriedade tipo, bool comPredicado) =>
        Obrigatoriedade.DaForma(tipo, comPredicado ? Condicao : null).Should().BeNull();

    [Theory(DisplayName = "A entrada lê o código exato da forma: o desconhecido, em minúscula ou ausente não é obrigatoriedade")]
    [InlineData("sempre")]
    [InlineData("SEMPRE ")]
    [InlineData("NENHUMA")]
    [InlineData(null)]
    public void EntradaDeRegras_CodigoNaoReconhecido_DevolveNulo(string? codigo) =>
        EntradaDeRegras.Obrigatoriedade(codigo, null).Should().BeNull();

    [Fact(DisplayName = "A entrada lê o código QUANDO com o predicado")]
    public void EntradaDeRegras_QuandoComPredicado_LeAObrigatoriedade() =>
        EntradaDeRegras.Obrigatoriedade("QUANDO", Condicao)!.Tipo.Should().Be(TipoObrigatoriedade.Quando);
}
