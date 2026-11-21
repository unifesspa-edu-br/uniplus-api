namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class FaseCanonicaCatalogoTests
{
    [Fact(DisplayName = "Constante da solicitação de isenção pertence ao rol canônico")]
    public void Catalogo_CodigoSolicitacaoIsencao_EhCanonico()
    {
        FaseCanonicaCatalogo.Codigos.Should().Contain(FaseCanonicaCatalogo.CodigoSolicitacaoIsencao);
        FaseCanonicaCatalogo.EhCanonico(FaseCanonicaCatalogo.CodigoSolicitacaoIsencao).Should().BeTrue();
    }

    [Theory(DisplayName = "Só a fase de avaliação admite agrupar Etapas")]
    [InlineData("AVALIACAO", true)]
    [InlineData("INSCRICAO", false)]
    [InlineData("HOMOLOGACAO_INSCRICOES", false)]
    [InlineData("HABILITACAO", false)]
    [InlineData("avaliacao", false)]
    [InlineData("FASE_FORA_DO_CATALOGO", false)]
    [InlineData(null, false)]
    public void AdmiteAgruparEtapas_SoNaAvaliacao(string? codigo, bool esperado) =>
        FaseCanonicaCatalogo.AdmiteAgruparEtapas(codigo).Should().Be(esperado);

    [Theory(DisplayName = "Admite complementação só a homologação das inscrições, nunca a habilitação")]
    [InlineData("HOMOLOGACAO_INSCRICOES", true)]
    [InlineData("RECURSOS", false)]
    [InlineData("homologacao_inscricoes", false)]
    [InlineData("HABILITACAO", false)]
    [InlineData("INSCRICAO", false)]
    [InlineData("AVALIACAO", false)]
    [InlineData("FASE_FORA_DO_CATALOGO", false)]
    [InlineData(null, false)]
    public void AdmiteComplementacao_SoNasFasesDaLei(string? codigo, bool esperado) =>
        FaseCanonicaCatalogo.AdmiteComplementacao(codigo).Should().Be(esperado);

    [Fact(DisplayName = "As fases que admitem complementação são exatamente as listadas pelo catálogo")]
    public void AdmiteComplementacao_CoincideComAListaDoCatalogo() =>
        FaseCanonicaCatalogo.Codigos
            .Where(FaseCanonicaCatalogo.AdmiteComplementacao)
            .Should().BeEquivalentTo(FaseCanonicaCatalogo.CodigosComComplementacaoPermitida);
}
