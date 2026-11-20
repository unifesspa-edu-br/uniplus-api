namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio.ModeloDeCodigosDeTeste;

public sealed class CodigosDosEnumsNoDocumentoTests
{
    private const string ComponenteDosCodigos = "#/components/schemas/FaseCodigo";

    private static readonly Lazy<Task<DocumentoPublicado>> Documento = new(GeradorDeDocumentoDeTeste.GerarDosControladoresAsync);

    [Fact(DisplayName = "O campo de pedido, o filtro, os dois formatos e os itens de coleção referenciam o mesmo componente dos códigos")]
    public async Task TodoCampoQueCarregaOsCodigos_ResolveParaOMesmoComponente()
    {
        DocumentoPublicado documento = await Documento.Value;
        JsonNode[] campos =
        [
            documento.Propriedade(nameof(PedidoDeFase), "fase"),
            documento.Propriedade(nameof(PedidoDeFase), "fases")["items"]!,
            documento.Parametro("/api/fases/direto", "get", "fase")["schema"]!,
            documento.Parametro("/api/fases/filtro", "get", "faseDoFiltro")["schema"]!,
            documento.Propriedade(nameof(Fase), "codigo"),
            documento.Propriedade("FaseComDescricao", "codigo"),
        ];

        campos.Select(DocumentoPublicado.Referencia).Should().OnlyContain(referencia => referencia == ComponenteDosCodigos);
        campos.Select(documento.Resolver).Should().OnlyContain(resolvido => ReferenceEquals(resolvido, documento.Componente("FaseCodigo")));
    }

    [Fact(DisplayName = "Nenhum campo traz a lista de códigos embutida: o enum aparece só no componente dos códigos")]
    public async Task ListaDeCodigos_AparecePenasNoComponenteDosCodigos()
    {
        DocumentoPublicado documento = await Documento.Value;

        documento.SchemasComEnum().Should().Equal("/components/schemas/FaseCodigo");
    }

    [Fact(DisplayName = "O enum tem um só componente dos códigos, ao lado dos componentes dos formatos")]
    public async Task Enum_TemUmSoComponenteDosCodigos()
    {
        DocumentoPublicado documento = await Documento.Value;

        documento.Componentes.Where(nome => nome.EndsWith("Codigo", StringComparison.Ordinal)).Should().Equal("FaseCodigo");
        documento.Componentes.Should().Contain([nameof(Fase), "FaseComDescricao"]);
    }

    [Fact(DisplayName = "O componente dos códigos é texto com os códigos na ordem declarada, a explicação do enum e a de cada código")]
    public async Task ComponenteDosCodigos_TextoComCodigosNaOrdemDeclaradaEExplicacaoDoEnumEDeCadaCodigo()
    {
        DocumentoPublicado documento = await Documento.Value;

        JsonNode codigos = documento.Componente("FaseCodigo");

        codigos["type"]!.GetValue<string>().Should().Be("string");
        codigos["enum"]!.AsArray().Select(static c => c!.GetValue<string>()).Should().Equal("inscricao", "resultado");
        codigos["description"]!.GetValue<string>().Should().Be(
            "Em que fase o processo está, para todos os códigos.\n\n"
            + "- `inscricao`: Inscrição — A pessoa ainda pode se inscrever.\n- `resultado`: Resultado — O resultado já saiu.");
    }

    [Fact(DisplayName = "A ordem em que o gerador visita os tipos não muda o componente dos códigos")]
    public async Task OrdemDeVisitaDosTipos_NaoMudaOComponenteDosCodigos()
    {
        DocumentoPublicado respostaPrimeiro = await GeradorDeDocumentoDeTeste.GerarAsync(app => MapearPedido(MapearResposta(app)));
        DocumentoPublicado pedidoPrimeiro = await GeradorDeDocumentoDeTeste.GerarAsync(app => MapearResposta(MapearPedido(app)));

        JsonNode.DeepEquals(respostaPrimeiro.Componente("FaseCodigo"), pedidoPrimeiro.Componente("FaseCodigo")).Should().BeTrue();
        respostaPrimeiro.Componentes.Order(StringComparer.Ordinal).Should().Equal(pedidoPrimeiro.Componentes.Order(StringComparer.Ordinal));
    }

    [Fact(DisplayName = "O documento gerado de novo pela mesma aplicação continua com o componente dos códigos e as referências")]
    public async Task DocumentoGeradoDeNovo_ContinuaComOComponenteEAsReferencias()
    {
        (DocumentoPublicado primeira, DocumentoPublicado segunda) = await GeradorDeDocumentoDeTeste.GerarDuasVezesAsync(app =>
            MapearPedido(MapearResposta(app)));

        DocumentoPublicado.Referencia(segunda.Propriedade(nameof(Fase), "codigo")).Should().Be(ComponenteDosCodigos);
        DocumentoPublicado.Referencia(segunda.Propriedade(nameof(PedidoDeFase), "fase")).Should().Be(ComponenteDosCodigos);
        JsonNode.DeepEquals(segunda.Componente("FaseCodigo"), primeira.Componente("FaseCodigo")).Should().BeTrue();
    }

    private static WebApplication MapearResposta(WebApplication app)
    {
        app.MapGet("/resposta", () => new RespostaDeFase(CodigoComRotulo<Fase>.De(Fase.Inscricao), CodigoComDescricao<Fase>.De(Fase.Inscricao)));

        return app;
    }

    private static WebApplication MapearPedido(WebApplication app)
    {
        app.MapPost("/pedido", (PedidoDeFase corpo) => corpo);

        return app;
    }
}
