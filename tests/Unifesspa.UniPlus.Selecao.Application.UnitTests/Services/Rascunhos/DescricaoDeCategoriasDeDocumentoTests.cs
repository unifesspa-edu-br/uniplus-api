namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeCategoriasDeDocumentoTests
{
    private const string DocumentoComTresBancas = """
        { "cronograma": { "fases": [ { "chave": "a", "bancasRequeridas": [
            { "chave": "b1", "tipoBancaCodigo": "BANCA_ANALISE_DOCUMENTAL", "categoriasDocumentoCodigos": [ "RENDA", "ESCOLA_PUBLICA" ] },
            { "chave": "b2", "tipoBancaCodigo": "BANCA_ANALISE_DOCUMENTAL", "categoriasDocumentoCodigos": [ "RENDA", "CATEGORIA_DESATIVADA" ] },
            { "chave": "b3", "tipoBancaCodigo": "BANCA_ANALISE_DOCUMENTAL", "categoriasDocumentoCodigos": [ "RENDA" ] } ] } ] } }
        """;

    private readonly ICategoriaDocumentoReader _categorias = Substitute.For<ICategoriaDocumentoReader>();

    public DescricaoDeCategoriasDeDocumentoTests() =>
        _categorias.ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([Categoria("ESCOLA_PUBLICA", "Escola pública"), Categoria("RENDA", "Renda familiar")]);

    private static CategoriaDocumentoView Categoria(string codigo, string nome) => new(Guid.CreateVersion7(), codigo, nome, null, 1);

    [Fact(DisplayName = "Os nomes das categorias entram depois da lista de códigos, na ordem dos códigos do documento")]
    public async Task Descrever_BancaComCategoriasConhecidas_TrazOsNomesNaOrdemDosCodigos()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComTresBancas, new DescricaoDeCategoriasDeDocumento(_categorias));

        JsonNode? banca = documento["cronograma"]!["fases"]![0]!["bancasRequeridas"]![0];
        DocumentoDescritoDeTeste.Chaves(banca).Should().Equal("chave", "tipoBancaCodigo", "categoriasDocumentoCodigos", "categoriasDocumentoNomes");
        DocumentoDescritoDeTeste.Textos(banca!["categoriasDocumentoNomes"]).Should().Equal("Renda familiar", "Escola pública");
    }

    [Fact(DisplayName = "A banca com um código que o cadastro não conhece fica sem a lista de nomes, para ela nunca ficar com tamanho diferente da dos códigos")]
    public async Task Descrever_BancaComCategoriaSemCadastroVivo_OmiteAListaDeNomes()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComTresBancas, new DescricaoDeCategoriasDeDocumento(_categorias));

        JsonNode? banca = documento["cronograma"]!["fases"]![0]!["bancasRequeridas"]![1];
        banca!["categoriasDocumentoNomes"].Should().BeNull();
        DocumentoDescritoDeTeste.Textos(banca["categoriasDocumentoCodigos"]).Should().Equal("RENDA", "CATEGORIA_DESATIVADA");
    }

    [Fact(DisplayName = "O cadastro é lido uma vez para todas as bancas, com os códigos distintos")]
    public async Task Descrever_VariasBancas_LeOCadastroUmaVez()
    {
        await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComTresBancas, new DescricaoDeCategoriasDeDocumento(_categorias));

        await _categorias.Received(1).ListarVivasPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 3), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A banca sem categorias não consulta o cadastro nem ganha lista de nomes")]
    public async Task Descrever_BancaSemCategorias_NaoConsultaOCadastro()
    {
        const string documentoSemCategorias = """
            { "cronograma": { "fases": [ { "chave": "a", "bancasRequeridas": [
                { "chave": "b1", "tipoBancaCodigo": "BANCA_ANALISE_DOCUMENTAL", "categoriasDocumentoCodigos": [] } ] } ] } }
            """;

        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(documentoSemCategorias, new DescricaoDeCategoriasDeDocumento(_categorias));

        documento["cronograma"]!["fases"]![0]!["bancasRequeridas"]![0]!["categoriasDocumentoNomes"].Should().BeNull();
        await _categorias.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
    }

    [Fact(DisplayName = "A lista de nomes que o esquema não declara só de leitura não é escrita")]
    public async Task Descrever_ListaDeNomesNaoDeclaradaSomenteLeitura_NaoEscreve()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(
            DocumentoComTresBancas, FormaDosDescritivosDeTeste.Documento(somenteLeitura: false), new DescricaoDeCategoriasDeDocumento(_categorias));

        documento["cronograma"]!["fases"]![0]!["bancasRequeridas"]![0]!["categoriasDocumentoNomes"].Should().BeNull();
    }
}
