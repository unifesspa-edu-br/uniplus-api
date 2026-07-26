namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class IdempotenciaOperationTransformerTests
{
    [Theory]
    [InlineData("409")]
    [InlineData("413")]
    public async Task TransformAsync_Should_AddCorpoTipado_QuandoStatusENaoDeclaradoPelaAction(string status)
    {
        OpenApiOperation operation = new();

        await new IdempotenciaOperationTransformer().TransformAsync(
            operation, Context(nameof(ControllerIdempotenteFicticio.ActionIdempotente)), CancellationToken.None);

        operation.Responses.Should().ContainKey(status);
        OpenApiMediaType media = operation.Responses[status].Content!["application/problem+json"];
        media.Schema.Should().BeOfType<OpenApiSchemaReference>()
            .Which.Reference!.Id.Should().Be("ProblemDetails", "o filtro de idempotência emite ProblemDetails nesses status");
    }

    [Theory]
    [InlineData("400")]
    [InlineData("422")]
    public async Task TransformAsync_Should_AddSoDescricao_QuandoStatusNaoTemCorpoTipadoPeloFiltro(string status)
    {
        OpenApiOperation operation = new();

        await new IdempotenciaOperationTransformer().TransformAsync(
            operation, Context(nameof(ControllerIdempotenteFicticio.ActionIdempotente)), CancellationToken.None);

        operation.Responses.Should().ContainKey(status);
        operation.Responses[status].Content.Should().BeNullOrEmpty(
            "400 e 422 do filtro ficam descrição-only — a action que não declara o próprio deve fazê-lo, não o transformer");
    }

    [Fact]
    public async Task TransformAsync_Should_NotAddResponses_WhenActionIsNotIdempotent()
    {
        OpenApiOperation operation = new();

        await new IdempotenciaOperationTransformer().TransformAsync(
            operation, Context(nameof(ControllerIdempotenteFicticio.ActionNaoIdempotente)), CancellationToken.None);

        operation.Responses.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task TransformAsync_Should_NotOverwrite_WhenActionAlreadyDeclares409()
    {
        OpenApiResponse own = new() { Description = "Conflito de domínio específico" };
        OpenApiOperation operation = new() { Responses = new OpenApiResponses { ["409"] = own } };

        await new IdempotenciaOperationTransformer().TransformAsync(
            operation, Context(nameof(ControllerIdempotenteFicticio.ActionIdempotente)), CancellationToken.None);

        operation.Responses["409"].Description.Should().Be("Conflito de domínio específico");
        operation.Responses["409"].Content.Should().BeNullOrEmpty("a declaração própria da action não é sobrescrita nem ganha corpo");
        operation.Responses.Should().ContainKey("413");
    }

    private static OpenApiOperationTransformerContext Context(string nomeDaAction) =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = new ApiDescription
            {
                ActionDescriptor = new ControllerActionDescriptor
                {
                    MethodInfo = typeof(ControllerIdempotenteFicticio).GetMethod(nomeDaAction)!,
                    ControllerTypeInfo = typeof(ControllerIdempotenteFicticio).GetTypeInfo(),
                },
            },
        };

    private sealed class ControllerIdempotenteFicticio
    {
        [RequiresIdempotencyKey]
        public static void ActionIdempotente()
        {
        }

        public static void ActionNaoIdempotente()
        {
        }
    }
}
