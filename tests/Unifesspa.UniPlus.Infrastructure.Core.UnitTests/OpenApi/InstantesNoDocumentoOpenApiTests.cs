namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class InstantesNoDocumentoOpenApiTests
{
    private const string Documento = "instantes";

    private static readonly Lazy<Task<OpenApiDocument>> DocumentoGerado = new(GerarDocumentoAsync);

    private static readonly JsonSchemaType TextoAnulavel = JsonSchemaType.String | JsonSchemaType.Null;

    [Fact(DisplayName = "Instante de propriedade é publicado como texto date-time")]
    public async Task Propriedade_PublicadaComoTextoDateTime()
    {
        IOpenApiSchema schema = await PropriedadeDoCorpoAsync("simples");

        ConferirTextoDateTime(schema, JsonSchemaType.String);
    }

    [Fact(DisplayName = "Instante anulável de propriedade é publicado como texto date-time ou nulo")]
    public async Task PropriedadeAnulavel_PublicadaComoTextoDateTimeOuNulo()
    {
        IOpenApiSchema schema = await PropriedadeDoCorpoAsync("anulavel");

        ConferirTextoDateTime(schema, TextoAnulavel);
    }

    [Fact(DisplayName = "Instante de objeto aninhado é publicado como texto date-time")]
    public async Task ObjetoAninhado_PublicadoComoTextoDateTime()
    {
        OpenApiDocument documento = await DocumentoGerado.Value;

        ConferirTextoDateTime(documento.Components!.Schemas!["Aninhado"].Properties!["instante"], JsonSchemaType.String);
    }

    [Fact(DisplayName = "Lista de instantes publica o item como texto date-time")]
    public async Task Lista_PublicaOItemComoTextoDateTime()
    {
        IOpenApiSchema schema = await PropriedadeDoCorpoAsync("lista");

        schema.Type.Should().Be(JsonSchemaType.Array);
        ConferirTextoDateTime(schema.Items, JsonSchemaType.String);
    }

    [Fact(DisplayName = "Lista de instantes anuláveis publica o item como texto date-time ou nulo")]
    public async Task ListaAnulavel_PublicaOItemComoTextoDateTimeOuNulo()
    {
        IOpenApiSchema schema = await PropriedadeDoCorpoAsync("listaAnulavel");

        ConferirTextoDateTime(schema.Items, TextoAnulavel);
    }

    [Fact(DisplayName = "Dicionário de instantes publica o valor como texto date-time")]
    public async Task Dicionario_PublicaOValorComoTextoDateTime()
    {
        IOpenApiSchema schema = await PropriedadeDoCorpoAsync("mapa");

        ConferirTextoDateTime(schema.AdditionalProperties, JsonSchemaType.String);
    }

    [Theory(DisplayName = "Instante de query é publicado como texto date-time, sem a variante nula, anulável ou não")]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Query_PublicadaComoTextoDateTimeSemVarianteNula(int posicao)
    {
        OpenApiDocument documento = await DocumentoGerado.Value;
        IOpenApiParameter parametro = documento.Paths["/consulta"].Operations![HttpMethod.Get].Parameters![posicao];

        ConferirTextoDateTime(parametro.Schema, JsonSchemaType.String);
    }

    private static async Task<IOpenApiSchema> PropriedadeDoCorpoAsync(string nome)
    {
        OpenApiDocument documento = await DocumentoGerado.Value;

        return documento.Components!.Schemas!["Corpo"].Properties![nome];
    }

    private static void ConferirTextoDateTime(IOpenApiSchema? schema, JsonSchemaType tipoEsperado)
    {
        schema.Should().NotBeNull();
        schema!.Type.Should().Be(tipoEsperado);
        schema.Format.Should().Be("date-time");
    }

    private static async Task<OpenApiDocument> GerarDocumentoAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddUniPlusJsonOptions();
        builder.Services.AddUniPlusOpenApi(Documento, builder.Configuration);
        await using WebApplication app = builder.Build();
        app.MapPost("/corpo", (Corpo corpo) => corpo);
        app.MapGet("/consulta", (DateTimeOffset? instante, DateTimeOffset obrigatorio) => instante);
        await app.StartAsync();

        return await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(Documento).GetOpenApiDocumentAsync();
    }

    public sealed record Corpo(
        DateTimeOffset Simples,
        DateTimeOffset? Anulavel,
        IReadOnlyList<DateTimeOffset> Lista,
        IReadOnlyList<DateTimeOffset?> ListaAnulavel,
        IReadOnlyDictionary<string, DateTimeOffset> Mapa,
        Aninhado Aninhado);

    public sealed record Aninhado(DateTimeOffset Instante);
}
