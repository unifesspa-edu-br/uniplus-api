namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// A lista que o contrato exige, omitida ou nula, é recusada na leitura do corpo, antes de existir comando: 400, código de
/// campo obrigatório ausente e o campo nomeado. É essa recusa que dispensa os validadores de repetir a checagem de nulo.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ListaObrigatoriaDoCorpoEndpointTests(CascadingFixture fixture)
{
    private const string Raiz = "/api/selecao/processos-seletivos/";
    private const string RaizAdmin = "/api/selecao/admin/processos-seletivos/";

    [Theory(DisplayName = "Prova presencial sem a lista de municípios é recusada nomeando o campo")]
    [InlineData("""{ "temProvaPresencial": true }""", true)]
    [InlineData("""{ "temProvaPresencial": true, "municipios": null }""", false)]
    public Task ProvaPresencial_SemMunicipios_Recusa(string corpo, bool omitido) =>
        RecusarAsync("prova-presencial", Raiz, corpo, "municipios", omitido);

    [Theory(DisplayName = "Cadastro de reserva sem a lista de chamadas é recusado nomeando o campo")]
    [InlineData("{ }", true)]
    [InlineData("""{ "chamadas": null }""", false)]
    public Task CadastroDeReserva_SemChamadas_Recusa(string corpo, bool omitido) =>
        RecusarAsync("cadastro-reserva", Raiz, corpo, "chamadas", omitido);

    [Theory(DisplayName = "Itens do formulário sem a lista de itens são recusados nomeando o campo")]
    [InlineData("{ }", true)]
    [InlineData("""{ "itens": null }""", false)]
    public Task Itens_SemALista_Recusa(string corpo, bool omitido) =>
        RecusarAsync("formularios/INSCRICAO/itens", RaizAdmin, corpo, "itens", omitido);

    // A lista de campos do grupo não é [JsonRequired]: omitida, chega nula e é recusada como o nulo.
    [Theory(DisplayName = "Grupo sem a lista de campos, omitida ou nula, é recusado nomeando o campo do grupo")]
    [InlineData("""{ "itens": [], "grupos": [{ "codigo": "COMPOSICAO", "ordem": 1, "rotulo": "Composição" }] }""")]
    [InlineData("""{ "itens": [], "grupos": [{ "codigo": "COMPOSICAO", "ordem": 1, "rotulo": "Composição", "subitens": null }] }""")]
    public Task Grupo_SemCampos_Recusa(string corpo) =>
        RecusarAsync("formularios/INSCRICAO/itens", RaizAdmin, corpo, "grupos[0].subitens", omitido: false);

    private async Task RecusarAsync(string recurso, string raiz, string corpo, string campo, bool omitido)
    {
        Guid processoId = await SemearAsync();
        using HttpRequestMessage request = CriarRequest($"{raiz}{processoId}/{recurso}", corpo);

        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await client.SendAsync(request);

        await (omitido
            ? RecusaDeCampoObrigatorioAusente.ConferirOmitidoAsync(resposta, campo)
            : RecusaDeCampoObrigatorioAusente.ConferirAsync(resposta, campo));
    }

    private static HttpRequestMessage CriarRequest(string caminho, string corpo)
    {
        HttpRequestMessage request = new(HttpMethod.Put, new Uri(caminho, UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return request;
    }

    private async Task<Guid> SemearAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (Domain.Entities.ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Lista obrigatória {Guid.CreateVersion7()}" });
        return processo.Id;
    }
}
