namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class BearerSecuritySchemeDocumentTransformerTests
{
    [Fact]
    public async Task TransformAsync_Should_DeclareHttpBearerJwtScheme()
    {
        OpenApiDocument document = new();

        await new BearerSecuritySchemeDocumentTransformer().TransformAsync(
            document, Context(), CancellationToken.None);

        IOpenApiSecurityScheme esquema = document.Components!
            .SecuritySchemes![BearerSecuritySchemeDocumentTransformer.SchemeName];

        esquema.Type.Should().Be(SecuritySchemeType.Http);
        esquema.Scheme.Should().Be("bearer");
        esquema.BearerFormat.Should().Be("JWT");
        esquema.Description.Should().NotBeNullOrWhiteSpace("o consumidor precisa saber de onde vem o token");
    }

    [Fact]
    public async Task TransformAsync_Should_PreserveExistingComponents()
    {
        // O transformer roda depois dos que povoam schemas (ProblemDetails e os DTOs). Substituir
        // Components em vez de completá-lo apagaria todos eles e deixaria as referências órfãs.
        OpenApiDocument document = new()
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                {
                    ["ProblemDetails"] = new OpenApiSchema(),
                },
            },
        };

        await new BearerSecuritySchemeDocumentTransformer().TransformAsync(
            document, Context(), CancellationToken.None);

        document.Components!.Schemas.Should().ContainKey("ProblemDetails");
        document.Components.SecuritySchemes.Should()
            .ContainKey(BearerSecuritySchemeDocumentTransformer.SchemeName);
    }

    private static OpenApiDocumentTransformerContext Context() =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            DescriptionGroups = [],
        };
}
