namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;

public sealed class CodigosDosDocumentosDoModeloTests
{
    [Fact]
    public void Ler_ConsequenciaDoDocumentoDesconhecida_RecusaOCampo() =>
        Recusas("DESCONHECIDO", "FEDERAL", "RESOLVIDO").Should().ContainSingle()
            .Which.Should().Be(("conjuntoDeDocumentos.documentosExigidos[0].indeferimento.consequenciaDoDescumprimento", ModeloFormularioErrorCodes.ConsequenciaDoDescumprimentoDesconhecida));

    [Fact]
    public void Ler_AbrangenciaDesconhecida_RecusaOCampo() =>
        Recusas("ELIMINA_DO_PROCESSO", "DESCONHECIDO", "RESOLVIDO").Should().ContainSingle()
            .Which.Should().Be(("conjuntoDeDocumentos.documentosExigidos[0].indeferimento.basesLegais[0].abrangencia", ModeloFormularioErrorCodes.AbrangenciaDesconhecida));

    [Fact]
    public void Ler_SituacaoDesconhecida_RecusaOCampo() =>
        Recusas("ELIMINA_DO_PROCESSO", "FEDERAL", "DESCONHECIDO").Should().ContainSingle()
            .Which.Should().Be(("conjuntoDeDocumentos.documentosExigidos[0].indeferimento.basesLegais[0].situacao", ModeloFormularioErrorCodes.SituacaoDaBaseLegalDesconhecida));

    [Fact]
    public void Ler_CodigosDesconhecidosDoGrupo_AcumulaNaOrdemDaLeitura()
    {
        BaseLegalDoModeloInput baseLegal = new("Lei de referência", "DESCONHECIDO", "DESCONHECIDO", null);
        GrupoDeDocumentosDoModeloInput grupo = new(1, new("DESCONHECIDO", [baseLegal], false), []);
        LeituraDoConteudo leitura = new(new Dictionary<string, string>(StringComparer.Ordinal));
        leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, new(null, [grupo])));

        leitura.Recusas.Select(static r => r.Field).Should().Equal(
            "conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento.basesLegais[0].abrangencia",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento.basesLegais[0].situacao",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento.consequenciaDoDescumprimento");
        leitura.Recusas.Select(static r => r.Error.Code).Should().Equal(
            ModeloFormularioErrorCodes.AbrangenciaDesconhecida,
            ModeloFormularioErrorCodes.SituacaoDaBaseLegalDesconhecida,
            ModeloFormularioErrorCodes.ConsequenciaDoDescumprimentoDesconhecida);
    }

    private static IEnumerable<(string? Field, string Code)> Recusas(string consequenciaDoDescumprimento, string abrangencia, string situacao)
    {
        BaseLegalDoModeloInput baseLegal = new("Lei de referência", abrangencia, situacao, null);
        DocumentoExigidoDoModeloInput documento = new("INSCRICAO", "DOCUMENTO", null, new(true, consequenciaDoDescumprimento, [baseLegal], false));
        LeituraDoConteudo leitura = new(new Dictionary<string, string>(StringComparer.Ordinal));
        leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, new([documento], null)));
        return leitura.Recusas.Select(static r => (r.Field, r.Error.Code));
    }
}
