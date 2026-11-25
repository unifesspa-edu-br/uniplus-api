namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

public sealed class EsperaEsgotadaPelaTravaOperationTransformerTests
{
    [Theory(DisplayName = "A mutação com Idempotency-Key declara o 503 tipado, sem o Retry-After, e manda enviar com uma nova chave")]
    [InlineData("PUT")]
    [InlineData("POST")]
    public async Task Mutacao_ComChave_DeclaraRecusaSemPrazo(string metodoHttp)
    {
        OpenApiOperation operation = new();

        await Transformar(operation, typeof(TravaMarcadaDeTesteController), nameof(TravaMarcadaDeTesteController.Mutar), metodoHttp);

        OpenApiResponse recusa = operation.Responses!["503"].Should().BeOfType<OpenApiResponse>().Subject;
        recusa.Content!["application/problem+json"].Schema.Should().BeOfType<OpenApiSchemaReference>()
            .Which.Reference!.Id.Should().Be("ProblemDetails");
        recusa.Headers.Should().BeNullOrEmpty();
        recusa.Description.Should().Contain("nova chave");
    }

    [Fact(DisplayName = "A mutação sem Idempotency-Key manda reler o estado e enviar de novo, sem falar em chave")]
    public async Task Mutacao_SemChave_NaoFalaEmChave()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, typeof(TravaMarcadaDeTesteController), nameof(TravaMarcadaDeTesteController.MutarSemChave), "POST");

        OpenApiResponse recusa = operation.Responses!["503"].Should().BeOfType<OpenApiResponse>().Subject;
        recusa.Headers.Should().BeNullOrEmpty();
        recusa.Description.Should().Contain("envie de novo").And.NotContain("chave");
    }

    [Fact(DisplayName = "A mutação marcada só no método declara a recusa mesmo que o controller não seja marcado")]
    public async Task Mutacao_ComMetodoMarcado_DeclaraRecusa()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, typeof(TravaSemMarcaDeTesteController), nameof(TravaSemMarcaDeTesteController.MutarMarcada), "PUT");

        operation.Responses.Should().ContainKey("503");
    }

    [Theory(DisplayName = "A leitura não declara a recusa, porque não trava a linha")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task Leitura_DeControllerMarcado_NaoDeclaraRecusa(string metodoHttp)
    {
        OpenApiOperation operation = new();

        await Transformar(operation, typeof(TravaMarcadaDeTesteController),
            metodoHttp == "GET" ? nameof(TravaMarcadaDeTesteController.Ler) : nameof(TravaMarcadaDeTesteController.LerHead), metodoHttp);

        operation.Responses.Should().BeNullOrEmpty();
    }

    [Fact(DisplayName = "A rota sem marca não declara a recusa")]
    public async Task Rota_SemMarca_NaoDeclaraRecusa()
    {
        OpenApiOperation operation = new();

        await Transformar(operation, typeof(TravaSemMarcaDeTesteController), nameof(TravaSemMarcaDeTesteController.Mutar), "PUT");

        operation.Responses.Should().BeNullOrEmpty();
    }

    [Fact(DisplayName = "O 503 que a action já declara é mantido como está")]
    public async Task Action_ComPropria503_MantemADescricao()
    {
        OpenApiResponse propria = new() { Description = "Indisponível por motivo próprio" };

        await Transformar(
            new OpenApiOperation { Responses = new OpenApiResponses { ["503"] = propria } },
            typeof(TravaMarcadaDeTesteController), nameof(TravaMarcadaDeTesteController.Mutar), "PUT");

        propria.Description.Should().Be("Indisponível por motivo próprio");
        propria.Content.Should().BeNullOrEmpty();
        propria.Headers.Should().BeNullOrEmpty();
    }

    private static Task Transformar(OpenApiOperation operation, Type controller, string acao, string metodoHttp) =>
        new EsperaEsgotadaPelaTravaOperationTransformer().TransformAsync(
            operation, Contexto(controller, acao, metodoHttp), CancellationToken.None);

    private static OpenApiOperationTransformerContext Contexto(Type controller, string acao, string metodoHttp) =>
        new()
        {
            DocumentName = "selecao",
            ApplicationServices = NSubstitute.Substitute.For<IServiceProvider>(),
            Description = Mvc.Descricao(controller, acao, metodoHttp),
        };

    private static readonly AcoesDoMvcDeTeste Mvc = new(typeof(TravaMarcadaDeTesteController), typeof(TravaSemMarcaDeTesteController));
}
