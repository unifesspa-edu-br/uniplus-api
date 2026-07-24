namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

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

    private static OpenApiOperationTransformerContext Context(IList<object> metadata) =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = new ApiDescription
            {
                ActionDescriptor = new ControllerActionDescriptor { EndpointMetadata = metadata },
            },
        };
}
