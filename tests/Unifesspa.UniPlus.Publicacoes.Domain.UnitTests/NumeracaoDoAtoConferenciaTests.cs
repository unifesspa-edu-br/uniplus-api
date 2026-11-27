namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>A conferência da numeração do ato: órgão, série, ano e número, todas as recusas na ordem do corpo.</summary>
public sealed class NumeracaoDoAtoConferenciaTests
{
    private static readonly NumeracaoDoAto Valida = new("CEPS", "EDITAL", 2026, "13");

    [Fact(DisplayName = "Numeração válida é aceita sem os espaços das pontas e com o número vazio como ausente")]
    public void Conferir_Valida_AceitaNormalizada()
    {
        ParteConferida<NumeracaoDoAto> parte = new NumeracaoDoAto(" CEPS ", " EDITAL ", 2026, "  ").Conferir();

        parte.EstaAceita.Should().BeTrue();
        parte.Valor.Should().Be(new NumeracaoDoAto("CEPS", "EDITAL", 2026, null));
    }

    [Fact(DisplayName = "Órgão em branco, série longa, ano zero e número longo são recusados juntos, na ordem do corpo")]
    public void Conferir_TodosInvalidos_AcumulaNaOrdemDoCorpo()
    {
        NumeracaoDoAto numeracao = new(" ", new string('s', 101), 0, new string('n', 61));

        ResumoDasRecusas.De(numeracao.Conferir().Recusas).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("serie", AtoNormativoErrorCodes.TextoInvalido),
            ("ano", AtoNormativoErrorCodes.AnoInvalido),
            ("numero", AtoNormativoErrorCodes.TextoInvalido));
    }

    [Theory(DisplayName = "Ano que não é maior que zero é recusado")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Conferir_AnoNaoPositivo_Recusa(int ano)
    {
        ParteConferida<NumeracaoDoAto> parte = (Valida with { Ano = ano }).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("ano", AtoNormativoErrorCodes.AnoInvalido));
        parte.Recusas[0].Error.Message.Should().Be("O ano da numeração deve ser maior que zero.");
    }

    [Fact(DisplayName = "Série no limite de 100 caracteres é aceita")]
    public void Conferir_SerieNoLimite_Aceita()
    {
        (Valida with { Serie = new string('s', 100) }).Conferir().EstaAceita.Should().BeTrue();
    }

    [Fact(DisplayName = "Conferida lança ArgumentException com o campo da primeira recusa")]
    public void Conferida_Invalida_LancaComOCampo()
    {
        Action acao = () => (Valida with { Orgao = "", Ano = 0 }).Conferida();

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("orgao");
    }

    [Fact(DisplayName = "Conferida devolve a numeração normalizada quando não há recusa")]
    public void Conferida_Valida_DevolveNormalizada()
    {
        (Valida with { Orgao = " CEPS " }).Conferida().Should().Be(Valida);
    }
}
