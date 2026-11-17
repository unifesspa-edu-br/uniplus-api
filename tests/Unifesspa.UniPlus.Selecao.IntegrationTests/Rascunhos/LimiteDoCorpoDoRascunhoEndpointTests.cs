namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O limite do corpo da criação do rascunho: o filtro de idempotência lê o corpo antes da action e usa o mesmo limite do formato
/// do documento, 2 MiB, em vez do limite global dos endpoints idempotentes.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class LimiteDoCorpoDoRascunhoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Rota = "/api/selecao/rascunhos-de-processos-seletivos";
    private const string DocumentoMinimo =
        """{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "Processo Seletivo de Exemplo", "unidadeAdministradoraCodigo": "CEPS" } }""";

    private readonly CascadingFixture _fixture;

    public LimiteDoCorpoDoRascunhoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(
            _fixture.Factory.Services, Leandro, perfil, EscopoDaConcessao.DaUnidade(UnidadesDeTeste.Ceps.OrigemId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "O documento de 1,5 MiB e o de exatamente 2 MiB são criados")]
    [InlineData(1_572_864)]
    [InlineData(LimitesDoDocumento.TamanhoMaximoEmBytes)]
    public async Task CorpoAteOLimiteDoFormato_Cria(int tamanhoDoCorpo)
    {
        using HttpResponseMessage resposta = await CriarAsync(DocumentoComTamanho(tamanhoDoCorpo));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Um byte além de 2 MiB recusa 413 com o código do filtro de idempotência")]
    public async Task UmByteAlemDoLimite_RecusaComOCodigoDoFiltro()
    {
        using HttpResponseMessage resposta = await CriarAsync(DocumentoComTamanho(LimitesDoDocumento.TamanhoMaximoEmBytes + 1));

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.idempotency.body_muito_grande");
    }

    private static string DocumentoComTamanho(int bytes) => DocumentoMinimo + new string(' ', bytes - DocumentoMinimo.Length);

    private async Task<HttpResponseMessage> CriarAsync(string corpo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Leandro);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString());
        request.Content = new StringContent(corpo, Encoding.UTF8, "application/json");

        return await client.SendAsync(request);
    }
}
