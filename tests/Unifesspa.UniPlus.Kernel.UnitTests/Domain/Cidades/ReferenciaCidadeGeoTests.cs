namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Cidades;

using System.Linq;

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

    [Fact(DisplayName = "Código, nome e UF ausentes ao mesmo tempo acumulam as três violações independentes")]
    public void Validar_TrioTotalmenteAusente_AcumulaAsTresViolacoes()
    {
        Result resultado = ReferenciaCidadeGeo.Validar(null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio,
            CidadeReferenciaErrorCodes.NomeObrigatorio,
            CidadeReferenciaErrorCodes.UfObrigatoria,
        ]);
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

    /// <summary>
    /// ADR-0023: mensagem de erro nunca ecoa o dado rejeitado. cidadeUf chega sem
    /// nenhum limite de tamanho validado até este ponto — um valor arbitrariamente
    /// grande não pode acabar refletido na mensagem de resposta.
    /// </summary>
    [Fact(DisplayName = "UF incoerente não ecoa o valor submetido na mensagem")]
    public void Validar_UfIncoerente_NaoEcoaValorSubmetidoNaMensagem()
    {
        string ufMuitoLonga = new('X', 500);

        Result resultado = ReferenciaCidadeGeo.Validar("1504208", "Marabá", ufMuitoLonga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().NotContain(ufMuitoLonga);
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

    [Fact(DisplayName = "Nome acima do tamanho máximo é rejeitado por formato (evita 500 no SaveChanges)")]
    public void Validar_NomeMuitoLongo_Rejeita()
    {
        string nomeLongo = new('A', ReferenciaCidadeGeo.NomeMaxLength + 1);

        Result resultado = ReferenciaCidadeGeo.Validar("1504208", nomeLongo, "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Nome com caractere nulo é rejeitado antes da persistência")]
    public void Validar_NomeComCaractereNulo_Rejeita()
    {
        string nomeComNulo = "Mara" + '\0' + "bá";

        Result resultado = ReferenciaCidadeGeo.Validar("1504208", nomeComNulo, "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.NomeCaractereNulo);
    }

    [Fact(DisplayName = "Nome exatamente no tamanho máximo é aceito (limite inclusivo)")]
    public void Validar_NomeNoLimite_Aceita()
    {
        string nomeNoLimite = new('A', ReferenciaCidadeGeo.NomeMaxLength);

        Result resultado = ReferenciaCidadeGeo.Validar("1504208", nomeNoLimite, "PA");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "EhValida é o predicado equivalente a Validar().IsSuccess")]
    public void EhValida_EspelhaValidar()
    {
        ReferenciaCidadeGeo.EhValida("1504208", "Marabá", "PA").Should().BeTrue();
        ReferenciaCidadeGeo.EhValida("150420", "Marabá", "PA").Should().BeFalse();
    }

    [Theory(DisplayName = "TemPrefixoDeUfValido aceita prefixos de UF reais")]
    [InlineData("1504208")]
    [InlineData("3550308")]
    public void TemPrefixoDeUfValido_PrefixoReal_Aceita(string codigoIbge)
    {
        ReferenciaCidadeGeo.TemPrefixoDeUfValido(codigoIbge).Should().BeTrue();
    }

    [Fact(DisplayName = "TemPrefixoDeUfValido rejeita prefixo fora do mapa de UF (lacuna 17-21)")]
    public void TemPrefixoDeUfValido_PrefixoInexistente_Rejeita()
    {
        ReferenciaCidadeGeo.TemPrefixoDeUfValido("2012345").Should().BeFalse();
    }

    [Theory(DisplayName = "EhUfValida aceita as 27 siglas de UF do Brasil")]
    [InlineData("PA")]
    [InlineData("SP")]
    [InlineData("DF")]
    public void EhUfValida_UfReal_Aceita(string uf)
    {
        ReferenciaCidadeGeo.EhUfValida(uf).Should().BeTrue();
    }

    [Fact(DisplayName = "EhUfValida rejeita sigla que não corresponde a nenhuma UF")]
    public void EhUfValida_UfInexistente_Rejeita()
    {
        ReferenciaCidadeGeo.EhUfValida("ZZ").Should().BeFalse();
    }
}
