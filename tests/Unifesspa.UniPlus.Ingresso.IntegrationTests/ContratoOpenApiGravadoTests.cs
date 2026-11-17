namespace Unifesspa.UniPlus.Ingresso.IntegrationTests;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

public sealed class ContratoOpenApiGravadoTests : IDisposable
{
    private const string Especificacao = """{"openapi":"3.1.0"}""";
    private const string EspecificacaoNormalizada = "{\n  \"openapi\": \"3.1.0\"\n}\n";

    private readonly string _diretorio = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_diretorio))
            Directory.Delete(_diretorio, recursive: true);
    }

    [Fact(DisplayName = "Especificação em execução igual ao contrato gravado não falha")]
    public async Task ConferirContraOCaminho_EspecificacaoIgualAoContrato_NaoFalha()
    {
        string contrato = await GravarContratoAsync("""{ "openapi": "3.1.0" }""");
        using HttpClient client = ClienteQueServe(Especificacao);

        Func<Task> conferir = () => ContratoOpenApiGravado.ConferirContraOCaminhoAsync(client, "/openapi.json", contrato, regerar: false);

        await conferir.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Especificação em execução diferente do contrato gravado falha com a mensagem de contrato alterado")]
    public async Task ConferirContraOCaminho_EspecificacaoDivergente_FalhaComMensagemDeContratoAlterado()
    {
        string contrato = await GravarContratoAsync("""{"openapi":"3.0.0"}""");
        using HttpClient client = ClienteQueServe(Especificacao);

        Func<Task> conferir = () => ContratoOpenApiGravado.ConferirContraOCaminhoAsync(client, "/openapi.json", contrato, regerar: false);

        await conferir.Should().ThrowAsync<Xunit.Sdk.XunitException>().WithMessage("*contrato OpenAPI mudou*");
    }

    [Fact(DisplayName = "Contrato gravado ausente falha com a mensagem de que ele precisa existir no repositório")]
    public async Task ConferirContraOCaminho_ContratoAusente_FalhaComMensagemDeContratoObrigatorio()
    {
        string contrato = Path.Join(_diretorio, "ausente.json");
        using HttpClient client = ClienteQueServe(Especificacao);

        Func<Task> conferir = () => ContratoOpenApiGravado.ConferirContraOCaminhoAsync(client, "/openapi.json", contrato, regerar: false);

        await conferir.Should().ThrowAsync<Xunit.Sdk.XunitException>().WithMessage("*precisa existir no repositório*");
    }

    [Fact(DisplayName = "Regerar cria o diretório e grava a especificação normalizada com quebra de linha final")]
    public async Task ConferirContraOCaminho_Regerar_CriaODiretorioEGravaAEspecificacaoNormalizada()
    {
        string contrato = Path.Join(_diretorio, "contracts", "openapi.json");
        using HttpClient client = ClienteQueServe(Especificacao);

        await ContratoOpenApiGravado.ConferirContraOCaminhoAsync(client, "/openapi.json", contrato, regerar: true);

        (await File.ReadAllTextAsync(contrato)).Should().Be(EspecificacaoNormalizada);
    }

    [Fact(DisplayName = "Contrato gravado informado com caminho absoluto é recusado")]
    public async Task Conferir_ContratoComCaminhoAbsoluto_Recusa()
    {
        using HttpClient client = ClienteQueServe(Especificacao);
        string absoluto = Path.Join(_diretorio, "openapi.json");

        Func<Task> conferir = () => ContratoOpenApiGravado.ConferirAsync(client, "/openapi.json", absoluto);

        await conferir.Should().ThrowAsync<ArgumentException>().WithParameterName("caminhoRelativo");
    }

    private static HttpClient ClienteQueServe(string corpo) =>
        new(new RespostaFixa(corpo)) { BaseAddress = new Uri("http://localhost") };

    private async Task<string> GravarContratoAsync(string conteudo)
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
