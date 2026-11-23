namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

public sealed class PublicacaoDasDatasDoDocumentoTests
{
    [Theory]
    [InlineData("escolhas", "PrazoDeEmissao.EscolhaNaoDeclarada", "ValidadeDoDocumento.EscolhaNaoDeclarada")]
    [InlineData("fase", "DataDeReferenciaDoDocumento.DataDaFaseIndefinida", "DataDeReferenciaDoDocumento.DataDaFaseIndefinida")]
    [InlineData("inscricao", "DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido", "DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido")]
    public void Publicar_DocumentoIncompleto_RecusaCadaCampo(string caso, string primeiro, string segundo)
    {
        PublicacaoDeDocumentoBuilder cenario = new();
        if (caso != "escolhas") cenario.SemDatas();

        Result<VersaoConfiguracao> resultado = cenario.ComDocumento(caso).Congelar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(causa => causa.Error.Code).Should().Contain([primeiro, segundo]);
        if (primeiro == segundo) resultado.Errors.Count(causa => causa.Error.Code == primeiro).Should().Be(2);
    }

    [Theory]
    [InlineData("escolhas", "EscolhaNaoDeclarada")]
    [InlineData("fase", "DataDaFaseIndefinida")]
    [InlineData("inscricao", "FimDasInscricoesIndefinido")]
    public void Publicar_DocumentoIncompleto_DeixaPendenteSomenteAExigenciaQueUsaOCampo(string caso, string sufixo)
    {
        PublicacaoDeDocumentoBuilder cenario = new();
        if (caso != "escolhas") cenario.SemDatas();

        Result<VersaoConfiguracao> resultado = cenario.ComDocumento(caso).ComDocumentoCompletoSemReferencia().Congelar();

        IEnumerable<FieldError> pendencias = resultado.Errors.Where(causa => causa.Error.Code.EndsWith(sufixo, StringComparison.Ordinal));
        pendencias.Should().NotBeEmpty().And.OnlyContain(causa => causa.Error.Message.Contains("Laudo médico", StringComparison.Ordinal));
    }
}
