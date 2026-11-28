namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// O caminho de cada exigência segue o corpo do pedido: a posição do item em cada lista que o caminho atravessa, sem
/// depender de quantos documentos os grupos anteriores têm.
/// </summary>
public sealed class CaminhosDasExigenciasTests
{
    private static readonly Guid Fase = Guid.CreateVersion7();

    private static RegraDeComprovacao Documento(int ordem) => RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(Fase), ordem).Value!;

    private static RegraDeComprovacao Grupo(int ordem, params RegraDeComprovacao[] itens) =>
        RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.TodosOsItens, null, null), ordem, [], itens).Value!;

    [Fact(DisplayName = "O caminho de cada regra segue a ordem em que as regras são enumeradas, com um itens[] por grupo atravessado")]
    public void DasRegras_GrupoDentroDeGrupo_UmItensPorNivel()
    {
        RegraDeComprovacao[] regras = [Grupo(0, Documento(0), Grupo(1, Documento(0), Documento(1))), Documento(1)];

        CaminhosDasExigencias.DasRegras(regras, "regrasDeComprovacao").Should().Equal(
            "regrasDeComprovacao[0]",
            "regrasDeComprovacao[0].itens[0]",
            "regrasDeComprovacao[0].itens[1]",
            "regrasDeComprovacao[0].itens[1].itens[0]",
            "regrasDeComprovacao[0].itens[1].itens[1]",
            "regrasDeComprovacao[1]");
    }

    [Fact(DisplayName = "Só a regra de documento tem caminho de documento, e é o da regra seguido de documento")]
    public void DosDocumentos_SoAsRegrasDeDocumento()
    {
        RegraDeComprovacao[] regras = [Grupo(0, Documento(0), Grupo(1, Documento(0), Documento(1))), Documento(1)];
        RegraDeComprovacao[] todas = [.. regras.SelectMany(static regra => regra.EnumerarRegraEItens())];

        CaminhosDasExigencias.DosDocumentos(todas, CaminhosDasExigencias.DasRegras(regras, "regrasDeComprovacao")).Should().Equal(
            "regrasDeComprovacao[0].itens[0].documento",
            "regrasDeComprovacao[0].itens[1].itens[0].documento",
            "regrasDeComprovacao[0].itens[1].itens[1].documento",
            "regrasDeComprovacao[1].documento");
    }

    [Fact(DisplayName = "O número de documentos de um grupo anterior não muda o caminho da regra seguinte")]
    public void DasRegras_GrupoAnteriorMaior_NaoMudaOCaminhoDaRegraSeguinte()
    {
        RegraDeComprovacao[] comDois = [Grupo(0, Documento(0), Documento(1)), Documento(1)];
        RegraDeComprovacao[] comQuatro = [Grupo(0, Documento(0), Documento(1), Documento(2), Documento(3)), Documento(1)];

        IReadOnlyList<string> comDoisItens = CaminhosDasExigencias.DasRegras(comDois, "regrasDeComprovacao");
        IReadOnlyList<string> comQuatroItens = CaminhosDasExigencias.DasRegras(comQuatro, "regrasDeComprovacao");

        comDoisItens[^1].Should().Be("regrasDeComprovacao[1]").And.Be(comQuatroItens[^1]);
    }

    [Theory(DisplayName = "Na lista única o caminho é a posição na lista")]
    [InlineData(0)]
    [InlineData(3)]
    public void NaLista_UmaPosicaoPorExigencia(int quantidade)
    {
        IReadOnlyList<string> caminhos = CaminhosDasExigencias.NaLista("exigencias", quantidade);

        caminhos.Should().Equal([.. Enumerable.Range(0, quantidade).Select(static posicao => $"exigencias[{posicao}]")]);
    }

    [Fact(DisplayName = "Caminhos em quantidade diferente da das regras são recusados em vez de apontar o item errado")]
    public void DosDocumentos_QuantidadeDiferente_Recusa()
    {
        RegraDeComprovacao[] regras = [Documento(0), Documento(1)];

        Action conferir = () => CaminhosDasExigencias.DosDocumentos(regras, ["regrasDeComprovacao[0]"]);

        conferir.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "As conferências das datas de referência recusam caminhos em quantidade diferente da das exigências")]
    public void DatasDeReferencia_QuantidadeDiferente_Recusa()
    {
        DocumentoExigido[] documentos = [Exigencia(Fase), Exigencia(Fase)];

        Action conjunto = () => DatasDeReferenciaNoCronograma.ConferirConjunto([], documentos, ["exigencias[0]"]);
        Action completude = () => DatasDeReferenciaNoCronograma.ConferirCompletude([], documentos, ["exigencias[0]", "exigencias[1]", "exigencias[2]"]);

        conjunto.Should().Throw<ArgumentException>();
        completude.Should().Throw<ArgumentException>();
    }
}
