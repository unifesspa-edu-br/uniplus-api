namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class UniPlusOperationTransformerTests
{
    [Fact]
    public async Task TransformAsync_Should_AddIdempotencyHeader_WhenActionHasAttribute()
    {
        UniPlusOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = CreateContext(metadata: [new RequiresIdempotencyKeyAttribute()]);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Parameters.Should().NotBeNull();
        operation.Parameters.Should().ContainSingle(p =>
            p.Name == "Idempotency-Key" && p.In == ParameterLocation.Header && p.Required);
    }

    [Fact]
    public async Task TransformAsync_Should_AddIdempotentExtension_WhenActionHasAttribute()
    {
        UniPlusOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = CreateContext(metadata: [new RequiresIdempotencyKeyAttribute()]);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Extensions.Should().ContainKey("x-uniplus-idempotent");
    }

    [Fact]
    public async Task TransformAsync_Should_NotMutate_WhenActionLacksAttribute()
    {
        UniPlusOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = CreateContext(metadata: []);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Parameters.Should().BeNull();
        operation.Extensions.Should().BeNull();
    }

    [Fact]
    public async Task TransformAsync_Should_DescribeIdempotencyKeyHeader_InPortuguese()
    {
        UniPlusOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = CreateContext(metadata: [new RequiresIdempotencyKeyAttribute()]);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        IOpenApiParameter? header = operation.Parameters!.SingleOrDefault(p => p.Name == "Idempotency-Key");
        header.Should().NotBeNull();
        header!.Description.Should().Contain("Chave opaca").And.Contain("ADR-0027");
    }

    private static OpenApiOperationTransformerContext CreateContext(IList<object> metadata)
    {
        ApiDescription description = new()
        {
            ActionDescriptor = new ControllerActionDescriptor
            {
                EndpointMetadata = metadata,
            },
        };

        return new OpenApiOperationTransformerContext
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = description,
        };
    }
}
