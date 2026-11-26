namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// Os tipos de valor que uma descrição de cadastro pode devolver ao documento, e a lista de códigos que ganha a lista de nomes ao lado.
/// </summary>
public sealed class DescricaoDoDocumentoDoRascunhoTests
{
    private const string DocumentoComBaseLegal = """{ "fada": { "aplica": true, "baseLegalCodigo": "BASE" } }""";

    private static IDescricaoDeCadastro DescricaoDaBaseLegal(params object?[] atributos)
    {
        IDescricaoDeCadastro descricao = Substitute.For<IDescricaoDeCadastro>();
        descricao.Cadastro.Returns(CamposDeContextoDoDocumento.BasesLegaisDaFada);
        descricao.LerAsync(Arg.Any<IReadOnlyCollection<(string Codigo, string? Complemento)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> { [("BASE", null)] = atributos });
        return descricao;
    }

    [Fact(DisplayName = "O valor em lista de textos entra como lista, na ordem em que a descrição o devolve")]
    public async Task Descrever_ValorEmListaDeTextos_EscreveAListaNaOrdem()
    {
        IDescricaoDeCadastro descricao = DescricaoDaBaseLegal("LEI", "Identificação", "Descrição", new List<string> { "b", "a" });

        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBaseLegal, descricao);

        documento["fada"]!["municipios"]!.ToJsonString().Should().Be("""["b","a"]""");
    }

    [Fact(DisplayName = "O valor em estrutura JSON é copiado para cada documento, sem repartir o mesmo nó")]
    public async Task Descrever_ValorEmEstruturaJson_CopiaOValorParaCadaDocumento()
    {
        JsonArray municipios = new(JsonValue.Create("Marabá"));
        IDescricaoDeCadastro descricao = DescricaoDaBaseLegal("LEI", "Identificação", "Descrição", municipios);

        JsonObject primeiro = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBaseLegal, descricao);
        JsonObject segundo = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBaseLegal, descricao);

        primeiro["fada"]!["municipios"].Should().NotBeSameAs(segundo["fada"]!["municipios"]);
        municipios.Parent.Should().BeNull();
    }

    [Fact(DisplayName = "O valor de tipo que o documento não sabe escrever é recusado, e não gravado pela metade")]
    public async Task Descrever_ValorDeTipoInesperado_Falha()
    {
        IDescricaoDeCadastro descricao = DescricaoDaBaseLegal("LEI", "Identificação", "Descrição", 3.5m);

        Func<Task> descrever = () => DocumentoDescritoDeTeste.DescreverAsync(DocumentoComBaseLegal, descricao);

        await descrever.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "A lista de códigos com um item que não é texto não é descrita nem consulta o cadastro")]
    public async Task Descrever_ListaDeCodigosComItemQueNaoETexto_Ignora()
    {
        IDescricaoDeCadastro descricao = Substitute.For<IDescricaoDeCadastro>();
        descricao.Cadastro.Returns(CamposDeContextoDoDocumento.CategoriasDeDocumento);
        const string documentoComItemNumerico = """
            { "cronograma": { "fases": [ { "chave": "a", "bancasRequeridas": [
                { "chave": "b", "tipoBancaCodigo": "BANCA", "categoriasDocumentoCodigos": [ "RENDA", 3 ] } ] } ] } }
            """;

        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(documentoComItemNumerico, descricao);

        documento["cronograma"]!["fases"]![0]!["bancasRequeridas"]![0]!["categoriasDocumentoNomes"].Should().BeNull();
        await descricao.DidNotReceiveWithAnyArgs().LerAsync(default!, default);
    }
}
