namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>O conjunto de documentos do modelo: o caminho de cada documento, avulso ou de grupo, e o que as condições citam.</summary>
public sealed class ConjuntoDeDocumentosDoModeloTests
{
    private static ConjuntoDeDocumentosDoModelo ConjuntoComAvulsosEGrupos() => new(
        [Documento("RG", Quando("DO_AVULSO_0")), Documento("CPF", Quando("DO_AVULSO_1"))],
        [
            GrupoDeDocumentos(1, Documento("LAUDO", Quando("DO_GRUPO_0"))),
            GrupoDeDocumentos(null, Documento("CERTIDAO"), Documento("HISTORICO", Quando("DO_GRUPO_1"))),
        ]);

    [Fact(DisplayName = "O modelo sem documentos não tem documento nem grupo")]
    public void Nenhum_SemDocumentosNemGrupos()
    {
        ConjuntoDeDocumentosDoModelo.Nenhum.ComCaminho().Should().BeEmpty();
        ConjuntoDeDocumentosDoModelo.Nenhum.FatosCitados().Should().BeEmpty();
        ConjuntoDeDocumentosDoModelo.Nenhum.Predicados().Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada documento tem o caminho dele: os avulsos primeiro, depois os de cada grupo")]
    public void ComCaminho_AvulsosEDepoisOsDeCadaGrupo() =>
        ConjuntoComAvulsosEGrupos().ComCaminho().Select(static c => (c.Caminho, c.Documento.TipoDocumentoCodigo)).Should().Equal(
            ("conjuntoDeDocumentos.documentosExigidos[0]", "RG"),
            ("conjuntoDeDocumentos.documentosExigidos[1]", "CPF"),
            ("conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0]", "LAUDO"),
            ("conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[0]", "CERTIDAO"),
            ("conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[1]", "HISTORICO"));

    [Fact(DisplayName = "Os fatos citados são os das condições de todos os documentos, avulsos e de grupo")]
    public void FatosCitados_TodosOsDocumentos() =>
        ConjuntoComAvulsosEGrupos().FatosCitados().Should().Equal("DO_AVULSO_0", "DO_AVULSO_1", "DO_GRUPO_0", "DO_GRUPO_1");

    [Fact(DisplayName = "A condição de cada documento leva o caminho completo do campo dela")]
    public void Predicados_CaminhoCompletoDaCondicao() =>
        ConjuntoComAvulsosEGrupos().Predicados().Select(static p => p.Caminho).Should().Equal(
            "conjuntoDeDocumentos.documentosExigidos[0].quando",
            "conjuntoDeDocumentos.documentosExigidos[1].quando",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0].quando",
            "conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[0].quando",
            "conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[1].quando");

    [Fact(DisplayName = "O grupo vazio de documentos não tem documento a percorrer")]
    public void ComCaminho_GrupoSemDocumentos_NaoTemDocumento() =>
        ConjuntoComGrupos(GrupoDeDocumentos(1)).ComCaminho().Should().BeEmpty();
}
