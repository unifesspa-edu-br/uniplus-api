namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class RespostaJsonOperationTransformerTests
{
    private const string Componente = "DocumentoDeExemplo";

    private static OpenApiOperation OperacaoCom200SemCorpo() => new()
    {
        Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "OK" } },
    };

    [Fact(DisplayName = "A action marcada põe no 200 o corpo JSON que referencia o componente")]
    public async Task ActionMarcada_Poe200ComReferenciaAoComponente()
    {
        OpenApiOperation operation = OperacaoCom200SemCorpo();

        await Transformar(operation, new RespostaJsonAttribute(Componente));

        OpenApiResponse resposta = operation.Responses!["200"].Should().BeOfType<OpenApiResponse>().Subject;
        resposta.Content!.Keys.Should().Equal("application/json");
        resposta.Content["application/json"].Schema.Should().BeOfType<OpenApiSchemaReference>().Subject.Reference.Id.Should().Be(Componente);
    }

    [Fact(DisplayName = "A referência da resposta é escrita como $ref para o componente")]
    public async Task ActionMarcada_EscreveORefParaOComponente()
    {
        OpenApiOperation operation = OperacaoCom200SemCorpo();

        await Transformar(operation, new RespostaJsonAttribute(Componente));

        string escrito = await operation.Responses!["200"].Content!["application/json"].Schema!.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1);
        JsonNode.DeepEquals(JsonNode.Parse(escrito), JsonNode.Parse("""{ "$ref": "#/components/schemas/DocumentoDeExemplo" }""")).Should().BeTrue(escrito);
    }

    [Fact(DisplayName = "A resposta 200 que já existe mantém a descrição e os cabeçalhos")]
    public async Task ActionMarcada_MantemODemaisDoRespostaExistente()
    {
        OpenApiOperation operation = OperacaoCom200SemCorpo();
        OpenApiResponse existente = (OpenApiResponse)operation.Responses!["200"];
        existente.Headers = new Dictionary<string, IOpenApiHeader> { ["ETag"] = new OpenApiHeader() };

        await Transformar(operation, new RespostaJsonAttribute(Componente));

        operation.Responses["200"].Should().BeSameAs(existente);
        existente.Description.Should().Be("OK");
        existente.Headers.Should().ContainKey("ETag");
    }

    [Fact(DisplayName = "Sem a resposta 200 declarada, ela é criada com o corpo")]
    public async Task ActionMarcadaSem200_CriaARespostaComCorpo()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, new RespostaJsonAttribute(Componente));

        operation.Responses!["200"].Content!.Should().ContainKey("application/json");
    }

    [Fact(DisplayName = "A action sem a marca não muda a resposta")]
    public async Task ActionSemMarca_NaoMudaAResposta()
    {
        OpenApiOperation operation = OperacaoCom200SemCorpo();

        await Transformar(operation, marca: null);

        operation.Responses!["200"].Content.Should().BeNullOrEmpty();
    }

    [Fact(DisplayName = "A operação e o contexto são obrigatórios")]
    public async Task ArgumentoNulo_Lanca()
    {
        RespostaJsonOperationTransformer transformer = new();

        Func<Task> semOperacao = () => transformer.TransformAsync(null!, Contexto(marca: null), CancellationToken.None);
        Func<Task> semContexto = () => transformer.TransformAsync(new OpenApiOperation(), null!, CancellationToken.None);

        await semOperacao.Should().ThrowAsync<ArgumentNullException>();
        await semContexto.Should().ThrowAsync<ArgumentNullException>();
    }

    private static Task Transformar(OpenApiOperation operation, RespostaJsonAttribute? marca) =>
        new RespostaJsonOperationTransformer().TransformAsync(operation, Contexto(marca), CancellationToken.None);

    private static OpenApiOperationTransformerContext Contexto(RespostaJsonAttribute? marca) =>
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
