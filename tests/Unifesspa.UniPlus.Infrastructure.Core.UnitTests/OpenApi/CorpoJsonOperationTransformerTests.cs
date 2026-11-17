namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class CorpoJsonOperationTransformerTests
{
    private const string Esquema = "https://exemplo.test/contracts/documento.schema.json";

    [Fact(DisplayName = "A action marcada declara o corpo JSON obrigatório, um objeto que aponta o esquema na documentação externa")]
    public async Task ActionMarcada_DeclaraCorpoJsonQueApontaOEsquema()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, new CorpoJsonAttribute(Esquema));

        OpenApiRequestBody corpo = operation.RequestBody.Should().BeOfType<OpenApiRequestBody>().Subject;
        corpo.Required.Should().BeTrue();
        OpenApiSchema schema = corpo.Content!["application/json"].Schema.Should().BeOfType<OpenApiSchema>().Subject;
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.ExternalDocs!.Url!.ToString().Should().Be(Esquema);
        schema.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "O corpo só referencia o esquema: não embute propriedades nem $ref")]
    public async Task ActionMarcada_NaoEmbuteOEsquema()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, new CorpoJsonAttribute(Esquema));

        OpenApiSchema schema = operation.RequestBody!.Content!["application/json"].Schema.Should().BeOfType<OpenApiSchema>().Subject;
        schema.Properties.Should().BeNullOrEmpty();
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
