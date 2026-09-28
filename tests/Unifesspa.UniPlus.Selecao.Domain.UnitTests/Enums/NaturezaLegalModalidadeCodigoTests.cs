namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O token que Seleção recebe de <c>ModalidadeView.NaturezaLegal</c> ao congelar uma
/// modalidade do catálogo. Um token que <see cref="NaturezaLegalModalidadeCodigo.FromCodigo"/>
/// não reconhece vira <see cref="NaturezaLegalModalidade.Nenhuma"/>, e a distribuição de
/// vagas passa a recusar a modalidade — por isso a volta precisa de prova própria, além da
/// ida que o fitness de arquitetura cruza com o catálogo.
/// </summary>
public sealed class NaturezaLegalModalidadeCodigoTests
{
    [Theory(DisplayName = "Os tokens do catálogo são lidos para a natureza correta")]
    [InlineData("COTA_RESERVADA", NaturezaLegalModalidade.CotaReservada)]
    [InlineData("AMPLA", NaturezaLegalModalidade.Ampla)]
    [InlineData("ACAO_AFIRMATIVA", NaturezaLegalModalidade.AcaoAfirmativa)]
    public void FromCodigo_TokenDoCatalogo_Resolve(string codigo, NaturezaLegalModalidade esperada) =>
        NaturezaLegalModalidadeCodigo.FromCodigo(codigo).Should().Be(esperada);

    [Fact(DisplayName = "Toda natureza não sentinela sobrevive à ida e volta ToCodigo → FromCodigo")]
    public void ToCodigo_FromCodigo_RoundTrip_TodasAsNaturezas()
    {
        foreach (NaturezaLegalModalidade natureza in Enum.GetValues<NaturezaLegalModalidade>()
            .Where(n => n != NaturezaLegalModalidade.Nenhuma))
        {
            NaturezaLegalModalidadeCodigo.FromCodigo(natureza.ToCodigo()).Should().Be(natureza);
        }
    }

    [Theory(DisplayName = "SUPLEMENTAR e OUTRA_MODALIDADE deixaram de ser natureza e viram o sentinela")]
    [InlineData("SUPLEMENTAR")]
    [InlineData("OUTRA_MODALIDADE")]
    [InlineData("AcaoAfirmativa")]
    [InlineData(null)]
    public void FromCodigo_ForaDoVocabulario_Nenhuma(string? codigo) =>
        NaturezaLegalModalidadeCodigo.FromCodigo(codigo).Should().Be(NaturezaLegalModalidade.Nenhuma);
}
