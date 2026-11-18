namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class CorpoJsonOperationTransformerTests
{
    private const string Componente = "DocumentoDeExemplo";

    [Fact(DisplayName = "A action marcada declara o corpo JSON obrigatório, uma referência ao componente")]
    public async Task ActionMarcada_DeclaraCorpoJsonQueReferenciaOComponente()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, new CorpoJsonAttribute(Componente));

        OpenApiRequestBody corpo = operation.RequestBody.Should().BeOfType<OpenApiRequestBody>().Subject;
        corpo.Required.Should().BeTrue();
        corpo.Content!.Keys.Should().Equal("application/json");
        OpenApiSchemaReference schema = corpo.Content["application/json"].Schema.Should().BeOfType<OpenApiSchemaReference>().Subject;
        schema.Reference.Id.Should().Be(Componente);
    }

    [Fact(DisplayName = "A referência do corpo é escrita como $ref para o componente, sem embutir o esquema")]
    public async Task ActionMarcada_EscreveORefParaOComponente()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, new CorpoJsonAttribute(Componente));

        string escrito = await operation.RequestBody!.Content!["application/json"].Schema!.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1);
        JsonNode.DeepEquals(JsonNode.Parse(escrito), JsonNode.Parse("""{ "$ref": "#/components/schemas/DocumentoDeExemplo" }""")).Should().BeTrue(escrito);
    }

    [Fact(DisplayName = "A action sem a marca não ganha corpo")]
    public async Task ActionSemMarca_NaoGanhaCorpo()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, marca: null);

        operation.RequestBody.Should().BeNull();
    }

    [Fact(DisplayName = "A operação e o contexto são obrigatórios")]
    public async Task ArgumentoNulo_Lanca()
    {
        CorpoJsonOperationTransformer transformer = new();

        Func<Task> semOperacao = () => transformer.TransformAsync(null!, Contexto(marca: null), CancellationToken.None);
        Func<Task> semContexto = () => transformer.TransformAsync(new OpenApiOperation(), null!, CancellationToken.None);

        await semOperacao.Should().ThrowAsync<ArgumentNullException>();
        await semContexto.Should().ThrowAsync<ArgumentNullException>();
    }

    private static Task Transformar(OpenApiOperation operation, CorpoJsonAttribute? marca) =>
        new CorpoJsonOperationTransformer().TransformAsync(operation, Contexto(marca), CancellationToken.None);

    private static OpenApiOperationTransformerContext Contexto(CorpoJsonAttribute? marca) =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = new ApiDescription
            {
                ActionDescriptor = new ActionDescriptor
                {
                    EndpointMetadata = marca is null ? [] : [marca],
                },
            },
        };
}
