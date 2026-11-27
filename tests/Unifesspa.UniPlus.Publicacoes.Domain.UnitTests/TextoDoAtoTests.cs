namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>A regra dos textos do ato: sem espaços nas pontas, até o limite, obrigatório ou opcional.</summary>
public sealed class TextoDoAtoTests
{
    [Theory(DisplayName = "Texto obrigatório nulo, vazio ou só de espaços é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Obrigatorio_Vazio_Recusa(string? valor)
    {
        ParteConferida<string> parte = TextoDoAto.Obrigatorio(valor, 10, "orgao", "O órgão");

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("orgao", AtoNormativoErrorCodes.TextoInvalido));
        parte.Recusas[0].Error.Message.Should().Be("O órgão é obrigatório e deve ter no máximo 10 caracteres.");
    }

    [Fact(DisplayName = "Texto obrigatório acima do limite é recusado sem repetir o valor")]
    public void Obrigatorio_AcimaDoLimite_RecusaSemRepetirOValor()
    {
        string valor = new('y', 11);

        ParteConferida<string> parte = TextoDoAto.Obrigatorio(valor, 10, "orgao", "O órgão");

        parte.EstaAceita.Should().BeFalse();
        parte.Recusas[0].Error.Message.Should().NotContain(valor);
    }

    [Fact(DisplayName = "Texto obrigatório no limite é aceito sem os espaços das pontas")]
    public void Obrigatorio_NoLimiteComEspacos_AceitoAparado()
    {
        ParteConferida<string> parte = TextoDoAto.Obrigatorio("  " + new string('x', 10) + " ", 10, "orgao", "O órgão");

        parte.EstaAceita.Should().BeTrue();
        parte.Valor.Should().Be(new string('x', 10));
    }

    [Theory(DisplayName = "Texto opcional vazio é aceito como ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Opcional_Vazio_AceitoComoAusente(string? valor)
    {
        ParteConferida<string> parte = TextoDoAto.Opcional(valor, 10, "numero", "O número");

        parte.EstaAceita.Should().BeTrue();
        parte.Valor.Should().BeNull();
    }

    [Fact(DisplayName = "Texto opcional acima do limite é recusado")]
    public void Opcional_AcimaDoLimite_Recusa()
    {
        ParteConferida<string> parte = TextoDoAto.Opcional(new string('x', 11), 10, "numero", "O número");

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("numero", AtoNormativoErrorCodes.TextoInvalido));
        parte.Recusas[0].Error.Message.Should().Be("O número deve ter no máximo 10 caracteres.");
    }

    [Fact(DisplayName = "Texto opcional no limite é aceito aparado")]
    public void Opcional_NoLimite_AceitoAparado()
    {
        TextoDoAto.Opcional(" " + new string('x', 10) + " ", 10, "numero", "O número").Valor
            .Should().Be(new string('x', 10));
    }
}
