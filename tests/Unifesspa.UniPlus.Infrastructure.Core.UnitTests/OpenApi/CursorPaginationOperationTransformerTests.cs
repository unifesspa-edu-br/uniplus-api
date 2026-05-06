namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

public sealed class CursorPaginationOperationTransformerTests
{
    private const string FixtureResource = "editais";

    [Fact(DisplayName = "Action com [FromCursor] remove AfterId/Limit vazados e adiciona cursor/limit como query params")]
    public async Task TransformAsync_Should_ReplaceLeakedParameters_WithWireParameters()
    {
        CursorPaginationOperationTransformer transformer = new();
        OpenApiOperation operation = OperationWithLeakedPageRequestParameters();
        OpenApiOperationTransformerContext context = ContextForActionWithCursor();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Parameters.Should().NotBeNull();
        operation.Parameters!.Select(p => p.Name).Should().NotContain(["AfterId", "Limit"]);
        operation.Parameters.Should().ContainSingle(p =>
            p.Name == "cursor" && p.In == ParameterLocation.Query && p.Required != true);
        operation.Parameters.Should().ContainSingle(p =>
            p.Name == "limit" && p.In == ParameterLocation.Query && p.Required != true);
    }

    [Fact(DisplayName = "Action com [FromCursor] declara Link (RFC 5988/8288) e X-Page-Size em response 200")]
    public async Task TransformAsync_Should_DeclareLinkAndPageSizeHeaders_OnOk()
    {
        CursorPaginationOperationTransformer transformer = new();
        OpenApiOperation operation = OperationWith200Response();
        OpenApiOperationTransformerContext context = ContextForActionWithCursor();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        IOpenApiResponse response200 = operation.Responses!["200"];
        OpenApiResponse okResponse = response200.Should().BeOfType<OpenApiResponse>().Subject;
        okResponse.Headers.Should().NotBeNull();

        IOpenApiHeader linkHeader = okResponse.Headers!["Link"];
        linkHeader.Description.Should().Contain("RFC 5988/8288").And.Contain("rel=\"next\"");
        linkHeader.Schema.Should().NotBeNull();
        linkHeader.Schema!.Type!.Value.HasFlag(JsonSchemaType.String).Should().BeTrue();

        IOpenApiHeader pageSizeHeader = okResponse.Headers["X-Page-Size"];
        pageSizeHeader.Schema.Should().NotBeNull();
        pageSizeHeader.Schema!.Type!.Value.HasFlag(JsonSchemaType.Integer).Should().BeTrue();
    }

    [Fact(DisplayName = "Action com [FromCursor] marca extension x-uniplus-paginated: true")]
    public async Task TransformAsync_Should_AddPaginatedExtension()
    {
        CursorPaginationOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = ContextForActionWithCursor();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Extensions.Should().ContainKey("x-uniplus-paginated");
    }

    [Fact(DisplayName = "Action sem [FromCursor] não é mutada (não adiciona parâmetros, headers ou extensions)")]
    public async Task TransformAsync_Should_NotMutate_WhenActionLacksFromCursor()
    {
        CursorPaginationOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse(),
            },
        };
        OpenApiOperationTransformerContext context = ContextForActionWithoutCursor();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Parameters.Should().BeNull();
        operation.Extensions.Should().BeNull();
        OpenApiResponse response200 = operation.Responses["200"].Should().BeOfType<OpenApiResponse>().Subject;
        response200.Headers.Should().BeNull(
            "transformer só declara Link/X-Page-Size em endpoints com [FromCursor]");
    }

    [Fact(DisplayName = "Action com [FromCursor] descreve cursor/limit em pt-BR e referencia ADR-0026")]
    public async Task TransformAsync_Should_DescribeWireParameters_InPortuguese()
    {
        CursorPaginationOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = ContextForActionWithCursor();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        IOpenApiParameter? cursorParam = operation.Parameters!.SingleOrDefault(p => p.Name == "cursor");
        cursorParam.Should().NotBeNull();
        cursorParam!.Description.Should().Contain("opaco").And.Contain("ADR-0026");

        IOpenApiParameter? limitParam = operation.Parameters!.SingleOrDefault(p => p.Name == "limit");
        limitParam.Should().NotBeNull();
        limitParam!.Description.Should().Contain("limit_invalido");
    }

    [Fact(DisplayName = "Action com [FromCursor] preserva outros query params do endpoint (não só remove os vazados)")]
    public async Task TransformAsync_Should_PreserveOtherQueryParameters()
    {
        CursorPaginationOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Parameters =
            [
                new OpenApiParameter { Name = "AfterId", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "Limit", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "status", In = ParameterLocation.Query },
            ],
        };
        OpenApiOperationTransformerContext context = ContextForActionWithCursor();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Parameters!.Select(p => p.Name).Should().Contain("status");
    }

    [Fact(DisplayName = "Action com [FromCursor] sem response 200 declarado não dispara exceção")]
    public async Task TransformAsync_Should_NotThrow_WhenResponse200IsAbsent()
    {
        CursorPaginationOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["404"] = new OpenApiResponse(),
            },
        };
        OpenApiOperationTransformerContext context = ContextForActionWithCursor();

        Func<Task> act = async () => await transformer.TransformAsync(operation, context, CancellationToken.None);

        await act.Should().NotThrowAsync();
        operation.Parameters!.Select(p => p.Name).Should().Contain(["cursor", "limit"]);
    }

    private static OpenApiOperation OperationWithLeakedPageRequestParameters() =>
        new()
        {
            Parameters =
            [
                new OpenApiParameter { Name = "AfterId", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "Limit", In = ParameterLocation.Query },
            ],
        };

    private static OpenApiOperation OperationWith200Response() =>
        new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse(),
            },
        };

    private static OpenApiOperationTransformerContext ContextForActionWithCursor() =>
        ContextForAction(typeof(FixtureController).GetMethod(nameof(FixtureController.Listar))!);

    private static OpenApiOperationTransformerContext ContextForActionWithoutCursor() =>
        ContextForAction(typeof(FixtureController).GetMethod(nameof(FixtureController.SemCursor))!);

    private static OpenApiOperationTransformerContext ContextForAction(MethodInfo methodInfo)
    {
        ControllerActionDescriptor descriptor = new()
        {
            EndpointMetadata = [],
            MethodInfo = methodInfo,
            Parameters = methodInfo.GetParameters()
                .Select(parameter => (Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor)
                    new ControllerParameterDescriptor
                    {
                        Name = parameter.Name!,
                        ParameterType = parameter.ParameterType,
                        ParameterInfo = parameter,
                    })
                .ToList(),
        };

        ApiDescription description = new()
        {
            ActionDescriptor = descriptor,
        };

        return new OpenApiOperationTransformerContext
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = description,
        };
    }

    private static class FixtureController
    {
#pragma warning disable IDE0060 // métodos fixture só servem para reflection
        public static Task Listar([FromCursor(FixtureResource)] PageRequest page) => Task.CompletedTask;

        public static Task SemCursor(int id) => Task.CompletedTask;
#pragma warning restore IDE0060
    }
}
