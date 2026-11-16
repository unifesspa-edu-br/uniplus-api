namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigoTermoConsentimentoTests
{
    [Theory(DisplayName = "O código nasce do nome, sem acento nem pontuação")]
    [InlineData("Termo de uso e privacidade", "TERMO_DE_USO_E_PRIVACIDADE")]
    [InlineData("  Autorização de consulta -- LGPD  ", "AUTORIZACAO_DE_CONSULTA_LGPD")]
    [InlineData("Declaração nº 12/2027", "DECLARACAO_NO_12_2027")]
    public void Gerar_FormaOCodigo(string nome, string esperado)
    {
        Result<CodigoTermoConsentimento> resultado = CodigoTermoConsentimento.Gerar(nome);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(esperado);
    }

    [Fact(DisplayName = "Nomes que só diferem em acento, caixa ou pontuação geram o mesmo código")]
    public void Gerar_AcentoCaixaEPontuacaoNaoDistinguem()
    {
        string a = CodigoTermoConsentimento.Gerar("Declaração de Veracidade").Value!.Valor;
        string b = CodigoTermoConsentimento.Gerar("declaracao de veracidade!").Value!.Valor;

        a.Should().Be(b);
    }

    [Theory(DisplayName = "Nome que não forma código no formato (sem letra, começando por dígito ou com um só caractere) é recusado")]
    [InlineData("---")]
    [InlineData("§ §")]
    [InlineData("123 Termo")]
    [InlineData("A")]
    [InlineData("2027")]
    public void Gerar_SemLetraNemDigito_Recusa(string nome)
    {
        Result<CodigoTermoConsentimento> resultado = CodigoTermoConsentimento.Gerar(nome);

        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.CodigoFormatoInvalido);
    }

    [Fact(DisplayName = "Nome que forma código acima de 100 caracteres é recusado")]
    public void Gerar_AcimaDoLimite_Recusa()
    {
        Result<CodigoTermoConsentimento> resultado = CodigoTermoConsentimento.Gerar(new string('a', 101));

        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.CodigoTamanho);
    }

    [Fact(DisplayName = "O nome que forma exatamente 100 caracteres é aceito")]
    public void Gerar_NoLimite_Aceita()
    {
        CodigoTermoConsentimento.Gerar(new string('a', 100)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar aceita o código no formato e recusa o vazio")]
    public void Criar_ValidaFormato()
    {
        CodigoTermoConsentimento.Criar(" TERMO_LGPD ").Value!.Valor.Should().Be("TERMO_LGPD");
        CodigoTermoConsentimento.Criar(null).Error!.Code.Should().Be(TermoConsentimentoErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Criar recusa código fora do formato")]
    [InlineData("termo_lgpd")]
    [InlineData("1TERMO")]
    [InlineData("A")]
    [InlineData("TERMO-LGPD")]
    public void Criar_ForaDoFormato_Recusa(string codigo)
    {
        CodigoTermoConsentimento.Criar(codigo).Error!.Code.Should().Be(TermoConsentimentoErrorCodes.CodigoFormatoInvalido);
    }
}
