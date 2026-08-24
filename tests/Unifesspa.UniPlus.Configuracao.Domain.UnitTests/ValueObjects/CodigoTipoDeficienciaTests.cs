namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigoTipoDeficienciaTests
{
    [Theory(DisplayName = "Códigos no formato fechado são aceitos e normalizados por Trim")]
    [InlineData("DEFICIENCIA_VISUAL")]
    [InlineData("TEA")]
    [InlineData("TEA_NIVEL_2")]
    [InlineData("AB")]
    [InlineData("D1")]
    public void Criar_FormatoValido_Aceita(string valor)
    {
        Result<CodigoTipoDeficiencia> resultado = CodigoTipoDeficiencia.Criar($"  {valor}  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Fact(DisplayName = "Código com exatamente 50 caracteres é aceito (limite superior inclusivo)")]
    public void Criar_TamanhoMaximo_Aceita()
    {
        string limite = "D" + new string('A', 49);

        Result<CodigoTipoDeficiencia> resultado = CodigoTipoDeficiencia.Criar(limite);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().HaveLength(50);
    }

    [Theory(DisplayName = "Códigos ausentes ou em branco retornam CodigoObrigatorio")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_EmBranco_Falha(string? valor)
    {
        Result<CodigoTipoDeficiencia> resultado = CodigoTipoDeficiencia.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoObrigatorio);
    }

    // Exemplos do Esquema de Cenário da task #1239 — um por motivo de recusa.
    [Theory(DisplayName = "Códigos fora do formato canônico retornam CodigoFormatoInvalido")]
    [InlineData("deficiencia_visual")]
    [InlineData("1_DEFICIENCIA")]
    [InlineData("DEFICIÊNCIA_VISUAL")]
    [InlineData("DEFICIENCIA-VISUAL")]
    [InlineData("D")]
    [InlineData("DEFICIENCIA_VISUAL_COM_NOME_LONGO_QUE_PASSA_DE_CINQUENTA")]
    [InlineData("_DEFICIENCIA")]
    public void Criar_FormatoInvalido_Falha(string valor)
    {
        Result<CodigoTipoDeficiencia> resultado = CodigoTipoDeficiencia.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "EhValido reflete o formato fechado sem alocar value object")]
    [InlineData("DEFICIENCIA_VISUAL", true)]
    [InlineData("  TEA  ", true)]
    [InlineData("deficiencia_visual", false)]
    [InlineData("", false)]
    [InlineData("D", false)]
    public void EhValido_RefleteFormato(string valor, bool esperado)
    {
        CodigoTipoDeficiencia.EhValido(valor).Should().Be(esperado);
    }

    [Fact(DisplayName = "ToString devolve o valor canônico")]
    public void ToString_DevolveValor()
    {
        CodigoTipoDeficiencia.Criar("TEA").Value!.ToString().Should().Be("TEA");
    }
}
