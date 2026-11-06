namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A recusa de entrada incompleta diz qual campo faltou, não só que a entrada é inválida.</summary>
public sealed class CamposObrigatoriosDaEntradaTests
{
    [Theory(DisplayName = "proteção do documento sem finalidade, hipótese legal ou classificação diz o que falta")]
    [InlineData(ClassificacaoDeProtecao.Publico, " ", "LGPD_ART7_II", "A proteção do tipo de documento precisa de finalidade do tratamento.")]
    [InlineData(ClassificacaoDeProtecao.Publico, "Habilitação", "", "A proteção do tipo de documento precisa de hipótese legal.")]
    [InlineData(ClassificacaoDeProtecao.Nenhuma, "Habilitação", "LGPD_ART7_II", "A classificação de proteção do tipo de documento é obrigatória.")]
    public void ProtecaoDoDocumento_Incompleta_NomeiaOQueFalta(
        ClassificacaoDeProtecao classificacao, string finalidade, string hipoteseLegal, string esperada)
    {
        ProtecaoDoDocumento protecao = new(classificacao, finalidade, hipoteseLegal);

        protecao.Violacao().Should().Be(esperada);
    }

    [Theory(DisplayName = "cópia do catálogo sem hipótese legal, finalidade ou descrição diz o que falta")]
    [InlineData("", "Habilitação", "Documento", "A cópia do catálogo precisa de hipótese legal.")]
    [InlineData("LGPD_ART7_II", " ", "Documento", "A cópia do catálogo precisa de finalidade do tratamento.")]
    [InlineData("LGPD_ART7_II", "Habilitação", "", "A cópia do catálogo precisa de descrição.")]
    public void CopiaDoItemNoCatalogo_Incompleta_NomeiaOQueFalta(string hipoteseLegal, string finalidade, string descricao, string esperada)
    {
        CopiaDoItemNoCatalogo copia = new(OrigemValoresColeta.Catalogo, null, "PUBLICO", hipoteseLegal, finalidade, descricao, null);

        copia.Violacao().Should().Be(esperada);
    }

    [Theory(DisplayName = "avaliação apurada sem inscrição, oferta ou grupo de área diz o que falta")]
    [InlineData(true, false, "TECNOLOGICA", "A avaliação precisa da inscrição.")]
    [InlineData(false, true, "TECNOLOGICA", "A avaliação precisa da oferta de curso.")]
    [InlineData(false, false, " ", "A avaliação precisa do código do grupo de área.")]
    public void AvaliacaoApurada_Incompleta_NomeiaOQueFalta(bool semInscricao, bool semOferta, string grupo, string esperada)
    {
        AvaliacaoApurada apurada = new(
            semInscricao ? Guid.Empty : Guid.CreateVersion7(),
            semOferta ? Guid.Empty : Guid.CreateVersion7(),
            grupo,
            SituacaoDaAvaliacao.EmConflito,
            null);

        apurada.Violacao().Should().Be(esperada);
    }

    [Theory(DisplayName = "avaliação apurada com nota fora de 0 a 1000 diz o limite")]
    [InlineData(-0.1)]
    [InlineData(1000.1)]
    public void AvaliacaoApurada_NotaForaDoIntervalo_DizOLimite(double nota)
    {
        AvaliacaoApurada apurada = new(Guid.CreateVersion7(), Guid.CreateVersion7(), "TECNOLOGICA", SituacaoDaAvaliacao.Apurada, (decimal)nota);

        apurada.Violacao().Should().Be("A nota fica entre 0 e 1000.");
    }
}
