namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

public sealed class ConsequenciaOpcionalNoModeloJsonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serializar_SemConsequencia_PreservaReenvioNoDocumentoENoGrupo(bool abrePrazo)
    {
        DocumentoExigidoDoModelo documento = new("INSCRICAO", "RG", null,
            new IndeferimentoDoDocumentoDoModelo(true, null, [], abrePrazo), RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos);
        GrupoDeDocumentosDoModelo grupo = new(1, new IndeferimentoDoGrupoDeDocumentosDoModelo(null, [], abrePrazo), [documento]);
        ConteudoDoModelo original = new(null, [], [], [], [], [], new([documento], [grupo]));
        ConteudoDoModelo lido = ConteudoDoModeloJson.Desserializar(ConteudoDoModeloJson.Serializar(original));
        lido.Documentos.DocumentosExigidos.Single().Indeferimento.ConsequenciaDoDescumprimento.Should().BeNull();
        lido.Documentos.DocumentosExigidos.Single().Indeferimento.AbrePrazoParaReenvio.Should().Be(abrePrazo);
        lido.Documentos.GruposDeDocumentos.Single().Indeferimento?.ConsequenciaDoDescumprimento.Should().BeNull();
        (lido.Documentos.GruposDeDocumentos.Single().Indeferimento?.AbrePrazoParaReenvio ?? false).Should().Be(abrePrazo);
    }
}
