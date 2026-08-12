namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class VendorMediaTypeOperationTransformerTests
{
    [Fact]
    public async Task TransformAsync_Should_ReplaceGenericMediaTypes_OnSuccessResponses()
    {
        // O ApiExplorer infere os media types do formatter JSON; o servidor, porém, negocia a
        // vendor MIME. Declarar os genéricos faz a interface de exploração escolher o primeiro
        // (text/plain) e receber 406 — e um cliente gerado nasce com o mesmo defeito.
        OpenApiOperation operation = ComResposta("200", "application/json", "text/json", "text/plain");

        await new VendorMediaTypeOperationTransformer().TransformAsync(
            operation, Context(new VendorMediaTypeAttribute { Resource = "edital", Versions = [1] }),
            CancellationToken.None);

        OpenApiResponse resposta = (OpenApiResponse)operation.Responses!["200"];
        resposta.Content.Should().ContainSingle()
            .Which.Key.Should().Be("application/vnd.uniplus.edital.v1+json");
    }

    [Fact]
    public async Task TransformAsync_Should_DeclareEveryAcceptedVersion()
    {
        OpenApiOperation operation = ComResposta("200", "application/json");

        await new VendorMediaTypeOperationTransformer().TransformAsync(
            operation, Context(new VendorMediaTypeAttribute { Resource = "edital", Versions = [1, 2] }),
            CancellationToken.None);

        ((OpenApiResponse)operation.Responses!["200"]).Content!.Keys.Should().BeEquivalentTo(
            "application/vnd.uniplus.edital.v1+json",
            "application/vnd.uniplus.edital.v2+json");
    }

    [Fact]
    public async Task TransformAsync_Should_PreserveSchema()
    {
        // Trocar o media type não pode custar o corpo declarado: sem o schema, o cliente gerado
        // deixa de ter o tipo de retorno.
        OpenApiOperation operation = ComResposta("200", "application/json");
        ((OpenApiResponse)operation.Responses!["200"]).Content!["application/json"].Schema =
            new OpenApiSchemaReference("EditalDto");

        await new VendorMediaTypeOperationTransformer().TransformAsync(
            operation, Context(new VendorMediaTypeAttribute { Resource = "edital", Versions = [1] }),
            CancellationToken.None);

        ((OpenApiResponse)operation.Responses["200"]).Content!["application/vnd.uniplus.edital.v1+json"]
            .Schema.Should().BeOfType<OpenApiSchemaReference>()
            .Which.Reference!.Id.Should().Be("EditalDto");
    }

    [Fact]
    public async Task TransformAsync_Should_NotTouchErrorResponses()
    {
        // O corpo de erro é ProblemDetails em qualquer versão do recurso (RFC 9457): versioná-lo
        // diria que a forma do erro muda com a versão, o que não acontece.
        OpenApiOperation operation = ComResposta("404", "application/problem+json");

        await new VendorMediaTypeOperationTransformer().TransformAsync(
            operation, Context(new VendorMediaTypeAttribute { Resource = "edital", Versions = [1] }),
            CancellationToken.None);

        ((OpenApiResponse)operation.Responses!["404"]).Content!.Keys.Should()
            .ContainSingle().Which.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task TransformAsync_Should_LeaveOperationUntouched_WhenThereIsNoVendorMediaType()
    {
        OpenApiOperation operation = ComResposta("200", "application/json");

        await new VendorMediaTypeOperationTransformer().TransformAsync(
            operation, Context(null), CancellationToken.None);

        ((OpenApiResponse)operation.Responses!["200"]).Content!.Keys.Should()
            .ContainSingle().Which.Should().Be("application/json");
    }

    private static OpenApiOperation ComResposta(string status, params string[] mediaTypes)
    {
        Dictionary<string, OpenApiMediaType> conteudo = new(StringComparer.Ordinal);
        foreach (string mediaType in mediaTypes)
        {
            conteudo[mediaType] = new OpenApiMediaType();
        }

        return new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                [status] = new OpenApiResponse { Description = "resposta", Content = conteudo },
            },
        };
    }

    private static OpenApiOperationTransformerContext Context(VendorMediaTypeAttribute? vendor) =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = new ApiDescription
            {
                ActionDescriptor = new ControllerActionDescriptor
                {
                    EndpointMetadata = vendor is null ? [] : [vendor],
                },
            },
            Document = new OpenApiDocument(),
        };
}
