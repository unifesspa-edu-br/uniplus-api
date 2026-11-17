namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Xunit;

/// <summary>
/// O campo da causa entregue ao cliente do documento tem o nome que o documento dá a ele: a tabela é por posição e o índice
/// de cada colchete atravessa a tradução.
/// </summary>
public sealed class CamposDoDocumentoTests
{
    [Theory(DisplayName = "O campo do corpo do pedido que o documento nomeia diferente é traduzido, com o índice de cada colchete preservado")]
    [InlineData("fases[3].faseCanonicaId", "cronograma.fases[3].faseCanonicaCodigo")]
    [InlineData("fases[0].faseConcluinteCodigo", "cronograma.fases[0].faseConcluinteChave")]
    [InlineData("fases[12].unidadeResponsavel", "cronograma.fases[12].unidadeResponsavelCodigo")]
    [InlineData("fases[1].bancasRequeridas[4].categoriasDocumentoIds", "cronograma.fases[1].bancasRequeridas[4].categoriasDocumentoCodigos")]
    [InlineData("etapas[2].faseCodigo", "etapas[2].faseChave")]
    [InlineData("etapas[7].id", "etapas[7].chave")]
    public void Traduzir_CampoComOutroNomeNoDocumento_UsaONomeDoDocumento(string campo, string esperado) =>
        CamposDoDocumento.Traduzir(campo).Should().Be(esperado);

    [Theory(DisplayName = "O campo que o documento nomeia igual só ganha o bloco do cronograma quando é de fase")]
    [InlineData("fases[0].ordem", "cronograma.fases[0].ordem")]
    [InlineData("fases[0].produtos[1].atoCodigo", "cronograma.fases[0].produtos[1].atoCodigo")]
    [InlineData("etapas[0].produtos[1].atoCodigo", "etapas[0].produtos[1].atoCodigo")]
    [InlineData("identificacao.nome", "identificacao.nome")]
    [InlineData("cronograma.fases", "cronograma.fases")]
    public void Traduzir_CampoComONomeDoDocumento_FicaComoEstaOuGanhaOBloco(string campo, string esperado) =>
        CamposDoDocumento.Traduzir(campo).Should().Be(esperado);

    [Theory(DisplayName = "O conceito da etapa, que o documento não tem, fica no trecho da etapa que o declara")]
    [InlineData("etapas[4].conceito", "etapas[4]")]
    [InlineData("etapas[4].conceito.faixas[1].nome", "etapas[4]")]
    [InlineData("etapas[4].conceitoDaNota", "etapas[4].conceitoDaNota")]
    public void Traduzir_ConceitoDaEtapa_ApontaAEtapa(string campo, string esperado) =>
        CamposDoDocumento.Traduzir(campo).Should().Be(esperado);

    [Fact(DisplayName = "A causa sem campo continua sem campo, e a tradução da lista mantém o código e a ordem")]
    public void Traduzir_Lista_MantemCodigoOrdemECausaSemCampo()
    {
        FieldError[] causas =
        [
            new("etapas[0].id", new DomainError("A.Um", "um")),
            new(null, new DomainError("A.Dois", "dois")),
            new("fases[1].unidadeResponsavel", new DomainError("A.Tres", "tres")),
        ];

        IReadOnlyList<FieldError> traduzidas = CamposDoDocumento.Traduzir(causas);

        traduzidas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("etapas[0].chave", "A.Um"), (null, "A.Dois"), ("cronograma.fases[1].unidadeResponsavelCodigo", "A.Tres"));
    }
}
