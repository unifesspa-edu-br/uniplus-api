namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class CorpoAcimaDoLimiteOperationTransformerTests
{
    [Fact(DisplayName = "A operação com corpo declara o 413 tipado, com o código do limite na descrição")]
    public async Task OperacaoComCorpo_Declara413Tipado()
    {
        OpenApiOperation operation = new() { RequestBody = new OpenApiRequestBody() };

        await Transformar(operation);

        OpenApiResponse recusa = operation.Responses!["413"].Should().BeOfType<OpenApiResponse>().Subject;
        recusa.Description.Should().Contain("uniplus.requisicao.corpo_acima_do_limite");
        recusa.Content!["application/problem+json"].Schema.Should().BeOfType<OpenApiSchemaReference>()
            .Which.Reference!.Id.Should().Be("ProblemDetails");
    }

    [Fact(DisplayName = "A operação sem corpo não declara o 413")]
    public async Task OperacaoSemCorpo_NaoDeclara413()
    {
        OpenApiOperation operation = new();

        await Transformar(operation);

        operation.Responses.Should().BeNullOrEmpty();
    }

    [Fact(DisplayName = "O 413 que a operação já declara não é sobrescrito")]
    public async Task Declaracao413DaOperacao_NaoESobrescrita()
    {
        OpenApiOperation operation = new()
        {
            RequestBody = new OpenApiRequestBody(),
            Responses = new OpenApiResponses { ["413"] = new OpenApiResponse { Description = "Própria da operação" } },
        };

        await Transformar(operation);

        operation.Responses["413"].Description.Should().Be("Própria da operação");
    }

    private static Task Transformar(OpenApiOperation operation) =>
        new CorpoAcimaDoLimiteOperationTransformer().TransformAsync(
            operation,
            new OpenApiOperationTransformerContext
            {
                DocumentName = "selecao",
                ApplicationServices = Substitute.For<IServiceProvider>(),
                Description = new Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription(),
            },
            CancellationToken.None);
}
