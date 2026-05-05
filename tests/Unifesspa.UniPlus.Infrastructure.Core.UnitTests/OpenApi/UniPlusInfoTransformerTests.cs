namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class UniPlusInfoTransformerTests
{
    [Theory]
    [InlineData("selecao", "Uni+ — Módulo Seleção")]
    [InlineData("ingresso", "Uni+ — Módulo Ingresso")]
    [InlineData("custom", "Uni+ — custom")]
    public async Task TransformAsync_Should_AssignTitlePerDocumentName(string documentName, string expectedTitle)
    {
        UniPlusInfoTransformer transformer = CreateTransformer();
        OpenApiDocument document = new();
        OpenApiDocumentTransformerContext context = CreateContext(documentName);

        await transformer.TransformAsync(document, context, CancellationToken.None);

        document.Info.Should().NotBeNull();
        document.Info.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public async Task TransformAsync_Should_AssignContractVersionFromOptions()
    {
        UniPlusInfoTransformer transformer = CreateTransformer(o => o with { ContractVersion = "1.2.3" });
        OpenApiDocument document = new();

        await transformer.TransformAsync(document, CreateContext("selecao"), CancellationToken.None);

        document.Info!.Version.Should().Be("1.2.3");
    }

    [Fact]
    public async Task TransformAsync_Should_AssignContactAndLicense()
    {
        UniPlusInfoTransformer transformer = CreateTransformer();
        OpenApiDocument document = new();

        await transformer.TransformAsync(document, CreateContext("selecao"), CancellationToken.None);

        document.Info!.Contact!.Email.Should().Be("ctic@unifesspa.edu.br");
        document.Info.Contact.Name.Should().Be("CTIC Unifesspa");
        document.Info.License!.Name.Should().Be("MIT");
    }

    [Fact]
    public async Task TransformAsync_Should_AssignProductionAndStagingServers()
    {
        UniPlusInfoTransformer transformer = CreateTransformer();
        OpenApiDocument document = new();

        await transformer.TransformAsync(document, CreateContext("selecao"), CancellationToken.None);

        document.Servers.Should().HaveCount(2);
        document.Servers![0].Description.Should().Be("Produção");
        document.Servers[1].Description.Should().Be("Homologação");
    }

    [Fact]
    public async Task TransformAsync_Should_DescribeApiInPortuguese()
    {
        UniPlusInfoTransformer transformer = CreateTransformer();
        OpenApiDocument document = new();

        await transformer.TransformAsync(document, CreateContext("selecao"), CancellationToken.None);

        document.Info!.Description.Should().Contain("Sistema Unificado Unifesspa");
        document.Info.Description.Should().Contain("pt-BR");
    }

    private static UniPlusInfoTransformer CreateTransformer(Func<UniPlusOpenApiOptions, UniPlusOpenApiOptions>? configure = null)
    {
        UniPlusOpenApiOptions baseOptions = new();
        UniPlusOpenApiOptions options = configure is null ? baseOptions : configure(baseOptions);
        return new UniPlusInfoTransformer(Options.Create(options));
    }

    private static OpenApiDocumentTransformerContext CreateContext(string documentName)
    {
        IServiceProvider services = Substitute.For<IServiceProvider>();
        return new OpenApiDocumentTransformerContext
        {
            DocumentName = documentName,
            ApplicationServices = services,
            DescriptionGroups = [],
        };
    }
}
