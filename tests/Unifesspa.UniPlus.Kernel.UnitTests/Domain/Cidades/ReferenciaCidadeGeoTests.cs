namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Cidades;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// CA-03 (#587): validação de formato + coerência de UF do
/// <c>cidade_codigo_ibge</c>, server-side, sem consultar o Geo nem validar
/// dígito verificador. Referência de cidade compartilhada (ADR-0090), promovida
/// ao Kernel para consumo cross-módulo (Configuração e Organização).
/// </summary>
public sealed class ReferenciaCidadeGeoTests
{
    [Fact(DisplayName = "Código de 7 dígitos com prefixo de UF coerente é aceito (Marabá/PA)")]
    public void Validar_CodigoCoerente_Aceita()
    {
        Result resultado = ReferenciaCidadeGeo.Validar("1504208", "Marabá", "PA");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Código com número de dígitos diferente de 7 é rejeitado por formato")]
    [InlineData("150420")]    // 6 dígitos
    [InlineData("15042080")]  // 8 dígitos
    public void Validar_QuantidadeDeDigitosInvalida_Rejeita(string codigo)
    {
        Result resultado = ReferenciaCidadeGeo.Validar(codigo, "Marabá", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Código com caractere não-numérico é rejeitado por formato")]
    public void Validar_CaractereNaoNumerico_Rejeita()
    {
        Result resultado = ReferenciaCidadeGeo.Validar("150420X", "Marabá", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Prefixo que não corresponde a UF válida é rejeitado por formato")]
    public void Validar_PrefixoUfInexistente_Rejeita()
    {
        // 20 não é código de UF válido (lacuna entre 17 e 21).
        Result resultado = ReferenciaCidadeGeo.Validar("2012345", "Cidade", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "UF incompatível com o prefixo do código é rejeitada (15=PA, não SP)")]
    public void Validar_UfIncoerente_Rejeita()
    {
        Result resultado = ReferenciaCidadeGeo.Validar("1504208", "Marabá", "SP");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "UF coerente em caixa diferente é aceita (case-insensitive)")]
    public void Validar_UfCaixaDiferente_Aceita()
    {
        Result resultado = ReferenciaCidadeGeo.Validar("1504208", "Marabá", "pa");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Campos obrigatórios vazios são rejeitados com o código apropriado")]
    [InlineData(null, "Marabá", "PA", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("", "Marabá", "PA", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("1504208", "", "PA", CidadeReferenciaErrorCodes.NomeObrigatorio)]
    [InlineData("1504208", "Marabá", "", CidadeReferenciaErrorCodes.UfObrigatoria)]
    public void Validar_CamposObrigatoriosVazios_Rejeita(string? codigo, string nome, string uf, string esperado)
    {
        Result resultado = ReferenciaCidadeGeo.Validar(codigo, nome, uf);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(esperado);
    }

    [Fact(DisplayName = "EhValida é o predicado equivalente a Validar().IsSuccess")]
    public void EhValida_EspelhaValidar()
    {
        ReferenciaCidadeGeo.EhValida("1504208", "Marabá", "PA").Should().BeTrue();
        ReferenciaCidadeGeo.EhValida("150420", "Marabá", "PA").Should().BeFalse();
    }
}
