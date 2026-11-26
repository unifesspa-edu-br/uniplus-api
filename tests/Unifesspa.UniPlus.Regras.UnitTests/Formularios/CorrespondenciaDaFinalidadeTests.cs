namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A tradução entre a finalidade gravada e a declaração do formulário (ADR-0171): a inscrição serve à
/// fase de inscrição e é obrigatória, a habilitação é obrigatória sem servir a ela, e a isenção da
/// taxa não é nenhuma das duas.
/// </summary>
public sealed class CorrespondenciaDaFinalidadeTests
{
    [Theory(DisplayName = "Cada finalidade corresponde à sua declaração")]
    [InlineData(FinalidadeFormulario.Inscricao, true, true)]
    [InlineData(FinalidadeFormulario.IsencaoTaxa, false, false)]
    [InlineData(FinalidadeFormulario.Habilitacao, false, true)]
    public void DeclaracaoDa_CadaFinalidade(FinalidadeFormulario finalidade, bool serveAFaseDeInscricao, bool obrigatorio) =>
        CorrespondenciaDaFinalidade.DeclaracaoDa(finalidade)
            .Should().Be(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio));

    [Fact(DisplayName = "A finalidade desconhecida não tem declaração")]
    public void DeclaracaoDa_FinalidadeDesconhecida_Recusa() =>
        FluentActions.Invoking(static () => CorrespondenciaDaFinalidade.DeclaracaoDa((FinalidadeFormulario)99))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact(DisplayName = "Sem finalidade, o formulário não declara nada")]
    public void DeclaracaoDa_SemFinalidade_NaoDeclara() =>
        CorrespondenciaDaFinalidade.DeclaracaoDa((FinalidadeFormulario?)null).Should().BeNull();

    [Fact(DisplayName = "A finalidade informada, como anulável, corresponde à mesma declaração")]
    public void DeclaracaoDa_FinalidadeAnulavelInformada_DeclaraComoAFinalidade() =>
        CorrespondenciaDaFinalidade.DeclaracaoDa((FinalidadeFormulario?)FinalidadeFormulario.Habilitacao)
            .Should().Be(CorrespondenciaDaFinalidade.DeclaracaoDa(FinalidadeFormulario.Habilitacao));

    [Theory(DisplayName = "A declaração corresponde à finalidade: servir à inscrição, inscrição; senão, obrigatório, habilitação; senão, isenção")]
    [InlineData(true, true, FinalidadeFormulario.Inscricao)]
    [InlineData(true, false, FinalidadeFormulario.Inscricao)]
    [InlineData(false, true, FinalidadeFormulario.Habilitacao)]
    [InlineData(false, false, FinalidadeFormulario.IsencaoTaxa)]
    public void FinalidadeDa_CadaDeclaracao(bool serveAFaseDeInscricao, bool obrigatorio, FinalidadeFormulario esperada) =>
        CorrespondenciaDaFinalidade.FinalidadeDa(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio))
            .Should().Be(esperada);

    [Theory(DisplayName = "A declaração de cada finalidade volta à mesma finalidade")]
    [InlineData(FinalidadeFormulario.Inscricao)]
    [InlineData(FinalidadeFormulario.IsencaoTaxa)]
    [InlineData(FinalidadeFormulario.Habilitacao)]
    public void FinalidadeDa_DeclaracaoDaFinalidade_VoltaAMesmaFinalidade(FinalidadeFormulario finalidade) =>
        CorrespondenciaDaFinalidade.FinalidadeDa(CorrespondenciaDaFinalidade.DeclaracaoDa(finalidade)).Should().Be(finalidade);
}
