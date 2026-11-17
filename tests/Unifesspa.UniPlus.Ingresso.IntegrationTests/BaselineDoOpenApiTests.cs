namespace Unifesspa.UniPlus.Ingresso.IntegrationTests;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

public sealed class BaselineDoOpenApiTests : IDisposable
{
    private const string Especificacao = """{"openapi":"3.1.0"}""";
    private const string EspecificacaoNormalizada = "{\n  \"openapi\": \"3.1.0\"\n}\n";

    private readonly string _diretorio = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_diretorio))
            Directory.Delete(_diretorio, recursive: true);
    }

    [Fact(DisplayName = "Spec em execução igual ao baseline não falha")]
    public async Task ConferirContraOCaminho_SpecIgualAoBaseline_NaoFalha()
    {
        string baseline = await GravarBaselineAsync("""{ "openapi": "3.1.0" }""");
        using HttpClient client = ClienteQueServe(Especificacao);

        Func<Task> conferir = () => BaselineDoOpenApi.ConferirContraOCaminhoAsync(client, "/openapi.json", baseline, regerar: false);

        await conferir.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Spec em execução diferente do baseline falha com a mensagem de contrato alterado")]
    public async Task ConferirContraOCaminho_SpecDivergente_FalhaComMensagemDeContratoAlterado()
    {
        string baseline = await GravarBaselineAsync("""{"openapi":"3.0.0"}""");
        using HttpClient client = ClienteQueServe(Especificacao);

        Func<Task> conferir = () => BaselineDoOpenApi.ConferirContraOCaminhoAsync(client, "/openapi.json", baseline, regerar: false);

        await conferir.Should().ThrowAsync<Xunit.Sdk.XunitException>().WithMessage("*contrato OpenAPI mudou*");
    }

    [Fact(DisplayName = "Baseline ausente falha com a mensagem de baseline que precisa estar committed")]
    public async Task ConferirContraOCaminho_BaselineAusente_FalhaComMensagemDeBaselineObrigatorio()
    {
        string baseline = Path.Join(_diretorio, "ausente.json");
        using HttpClient client = ClienteQueServe(Especificacao);

        Func<Task> conferir = () => BaselineDoOpenApi.ConferirContraOCaminhoAsync(client, "/openapi.json", baseline, regerar: false);

        await conferir.Should().ThrowAsync<Xunit.Sdk.XunitException>().WithMessage("*precisa estar committed*");
    }

    [Fact(DisplayName = "Regerar cria o diretório e grava a spec normalizada com quebra de linha final")]
    public async Task ConferirContraOCaminho_Regerar_CriaODiretorioEGravaASpecNormalizada()
    {
        string baseline = Path.Join(_diretorio, "contracts", "openapi.json");
        using HttpClient client = ClienteQueServe(Especificacao);

        await BaselineDoOpenApi.ConferirContraOCaminhoAsync(client, "/openapi.json", baseline, regerar: true);

        (await File.ReadAllTextAsync(baseline)).Should().Be(EspecificacaoNormalizada);
    }

    [Fact(DisplayName = "Baseline informado com caminho absoluto é recusado")]
    public async Task Conferir_BaselineComCaminhoAbsoluto_Recusa()
    {
        using HttpClient client = ClienteQueServe(Especificacao);
        string absoluto = Path.Join(_diretorio, "openapi.json");

        Func<Task> conferir = () => BaselineDoOpenApi.ConferirAsync(client, "/openapi.json", absoluto);

        await conferir.Should().ThrowAsync<ArgumentException>().WithParameterName("caminhoRelativo");
    }

    private static HttpClient ClienteQueServe(string corpo) =>
        new(new RespostaFixa(corpo)) { BaseAddress = new Uri("http://localhost") };

    private async Task<string> GravarBaselineAsync(string conteudo)
    {
        Directory.CreateDirectory(_diretorio);
        string caminho = Path.Join(_diretorio, "openapi.json");
        await File.WriteAllTextAsync(caminho, conteudo);
        return caminho;
    }

    private sealed class RespostaFixa(string corpo) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(ResponderComACorpo());

        private HttpResponseMessage ResponderComACorpo() => new(HttpStatusCode.OK) { Content = new StringContent(corpo) };
    }
}
