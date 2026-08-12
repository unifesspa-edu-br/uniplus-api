namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class AuthorizationOperationTransformerTests
{
    [Fact]
    public async Task TransformAsync_Should_Add401And403WithProblemDetails_WhenActionIsAuthorized()
    {
        OpenApiOperation operation = new();

        await new AuthorizationOperationTransformer().TransformAsync(
            operation, Context([new AuthorizeAttribute()]), CancellationToken.None);

        foreach (string status in new[] { "401", "403" })
        {
            operation.Responses.Should().ContainKey(status);
            OpenApiMediaType media = operation.Responses[status].Content!["application/problem+json"];
            media.Schema.Should().BeOfType<OpenApiSchemaReference>()
                .Which.Reference!.Id.Should().Be("ProblemDetails", "o servidor emite ProblemDetails nesses status");
        }
    }

    [Fact]
    public async Task TransformAsync_Should_NotAdd401Or403_WhenActionIsPublic()
    {
        OpenApiOperation operation = new();

        await new AuthorizationOperationTransformer().TransformAsync(
            operation, Context([]), CancellationToken.None);

        operation.Responses.Should().NotContainKey("401");
        operation.Responses.Should().NotContainKey("403");
    }

    [Fact]
    public async Task TransformAsync_Should_NotAdd401Or403_WhenAllowAnonymousIsPresent()
    {
        OpenApiOperation operation = new();

        // Authorize (from the class) + AllowAnonymous (from the method) in the aggregated metadata — AllowAnonymous wins.
        await new AuthorizationOperationTransformer().TransformAsync(
            operation, Context([new AuthorizeAttribute(), new AllowAnonymousAttribute()]), CancellationToken.None);

        operation.Responses.Should().NotContainKey("401", "AllowAnonymous vence o Authorize");
        operation.Responses.Should().NotContainKey("403");
    }

    [Fact]
    public async Task TransformAsync_Should_NotOverwrite_WhenActionAlreadyDeclares403()
    {
        OpenApiResponse own = new() { Description = "Descrição própria da action" };
        OpenApiOperation operation = new() { Responses = new OpenApiResponses { ["403"] = own } };

        await new AuthorizationOperationTransformer().TransformAsync(
            operation, Context([new AuthorizeAttribute()]), CancellationToken.None);

        operation.Responses["403"].Description.Should().Be("Descrição própria da action");
        operation.Responses.Should().ContainKey("401");
    }

    [Fact]
    public async Task TransformAsync_Should_RequireBearerScheme_WhenActionIsAuthorized()
    {
        OpenApiOperation operation = new();

        await new AuthorizationOperationTransformer().TransformAsync(
            operation, Context([new AuthorizeAttribute()]), CancellationToken.None);

        operation.Security.Should().ContainSingle(
            "declarar 401 sem dizer que credencial o evita descreveria o sintoma e omitiria o contrato");
        operation.Security![0].Keys.Should().ContainSingle()
            .Which.Should().BeOfType<OpenApiSecuritySchemeReference>()
            .Which.Reference!.Id.Should().Be(BearerSecuritySchemeDocumentTransformer.SchemeName);
    }

    [Fact]
    public async Task TransformAsync_Should_NotRequireBearerScheme_WhenAllowAnonymousIsPresent()
    {
        OpenApiOperation operation = new();

        await new AuthorizationOperationTransformer().TransformAsync(
            operation, Context([new AuthorizeAttribute(), new AllowAnonymousAttribute()]), CancellationToken.None);

        operation.Security.Should().BeNullOrEmpty("uma rota anônima exigir credencial mentiria sobre o servidor");
    }

    [Fact]
    public async Task TransformAsync_Should_SerializeSchemeName_InThePublishedDocument()
    {
        // Verificar só o Reference.Id em memória não basta: uma referência sem documento
        // hospedeiro guarda o id, passa nessa checagem e mesmo assim serializa como objeto
        // vazio ({}) — o contrato publicado fica sintaticamente válido e semanticamente mudo,
        // e nenhuma UI descobre que a rota aceita Bearer. Quem prova o comportamento é o JSON.
        OpenApiDocument document = new();
        await new BearerSecuritySchemeDocumentTransformer().TransformAsync(
            document, DocumentContext(), CancellationToken.None);

        OpenApiOperation operation = new();
        await new AuthorizationOperationTransformer().TransformAsync(
            operation, Context([new AuthorizeAttribute()], document), CancellationToken.None);

        document.Paths = new OpenApiPaths
        {
            ["/api/recurso"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Post] = operation },
            },
        };

        await using StringWriter texto = new();
        document.SerializeAsV31(new OpenApiJsonWriter(texto));

        // Procurar o nome do esquema no JSON inteiro não distinguiria nada: ele aparece em
        // components.securitySchemes de qualquer forma. O que precisa ser verdade é o
        // requisito DA OPERAÇÃO nomeá-lo.
        using JsonDocument publicado = JsonDocument.Parse(texto.ToString());
        JsonElement security = publicado.RootElement
            .GetProperty("paths").GetProperty("/api/recurso")
            .GetProperty("post").GetProperty("security");

        security.EnumerateArray().Should().ContainSingle()
            .Which.EnumerateObject().Select(p => p.Name).Should().Contain(
                BearerSecuritySchemeDocumentTransformer.SchemeName,
                "um requisito de segurança sem nome de esquema é sintaticamente válido e semanticamente mudo");
    }

    [Fact]
    public async Task TransformAsync_Should_NotDuplicateRequirement_WhenBearerIsAlreadyDeclared()
    {
        // O pipeline pode transformar a mesma operação mais de uma vez (documento por módulo);
        // sem o guard, cada passagem empilharia um requisito idêntico no contrato publicado.
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = Context([new AuthorizeAttribute()]);

        await new AuthorizationOperationTransformer().TransformAsync(operation, context, CancellationToken.None);
        await new AuthorizationOperationTransformer().TransformAsync(operation, context, CancellationToken.None);

        operation.Security.Should().ContainSingle();
    }

    private static OpenApiOperationTransformerContext Context(
        IList<object> metadata,
        OpenApiDocument? document = null) =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = new ApiDescription
            {
                ActionDescriptor = new ControllerActionDescriptor { EndpointMetadata = metadata },
            },

            // A referência de segurança resolve o esquema contra o documento hospedeiro ao
            // serializar; sem ele o requisito viraria um objeto vazio no contrato publicado.
            Document = document ?? new OpenApiDocument(),
        };

    private static OpenApiDocumentTransformerContext DocumentContext() =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            DescriptionGroups = [],
        };
}
