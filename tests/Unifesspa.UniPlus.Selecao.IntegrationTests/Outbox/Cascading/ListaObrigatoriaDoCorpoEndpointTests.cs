namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// A lista que o contrato exige, omitida ou nula, é recusada: na leitura do corpo, antes de existir comando, com 400, código de
/// campo obrigatório ausente e o campo nomeado; ou pelo domínio, com 422 e o campo nomeado, quando o contrato a publica como
/// obrigatória sem a exigir na leitura. É essa recusa que dispensa os validadores de repetir a checagem de nulo.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ListaObrigatoriaDoCorpoEndpointTests(CascadingFixture fixture)
{
    private const string Raiz = "/api/selecao/processos-seletivos/";
    private const string RaizAdmin = "/api/selecao/admin/processos-seletivos/";

    [Theory(DisplayName = "Itens do formulário sem a lista de itens são recusados pelo domínio nomeando o campo")]
    [InlineData("{ }")]
    [InlineData("""{ "itens": null }""")]
    public Task Itens_SemALista_Recusa(string corpo) =>
        RecusarNoDominioAsync("formularios/INSCRICAO/itens", corpo, "itens", "uniplus.validacao.obrigatorio");

    [Theory(DisplayName = "Termos do formulário sem a lista de termos são recusados pelo domínio nomeando o campo")]
    [InlineData("{ }")]
    [InlineData("""{ "termos": null }""")]
    public Task Termos_SemALista_Recusa(string corpo) =>
        RecusarNoDominioAsync("formularios/INSCRICAO/termos", corpo, "termos", "uniplus.selecao.termo_exigido_formulario.entrada_malformada");

    // A lista de campos do grupo não é [JsonRequired]: omitida, chega nula ao domínio, que a recusa como obrigatória junto das
    // demais recusas do grupo (as ocorrências também faltam no corpo).
    [Theory(DisplayName = "Grupo sem a lista de campos, omitida ou nula, é recusado pelo domínio nomeando o campo do grupo")]
    [InlineData("""{ "itens": [], "grupos": [{ "codigo": "COMPOSICAO", "ordem": 1, "rotulo": "Composição" }] }""")]
    [InlineData("""{ "itens": [], "grupos": [{ "codigo": "COMPOSICAO", "ordem": 1, "rotulo": "Composição", "subitens": null }] }""")]
    public Task Grupo_SemCampos_Recusa(string corpo) => RecusarComOCampoAsync(corpo, "grupos[0].subitens");

    [Theory(DisplayName = "Item do formulário sem a ordem, omitida ou nula, é recusado pelo domínio nomeando o campo do item")]
    [InlineData("""{ "itens": [{ "fatoCodigo": "NOME", "apresentacao": { "rotulo": "Nome", "tipoDeCampo": "SIM_OU_NAO" } }] }""")]
    [InlineData("""{ "itens": [{ "fatoCodigo": "NOME", "ordem": null, "apresentacao": { "rotulo": "Nome", "tipoDeCampo": "SIM_OU_NAO" } }] }""")]
    public Task Item_SemOrdem_Recusa(string corpo) => RecusarComOCampoAsync(corpo, "itens[0].ordem");

    private async Task RecusarComOCampoAsync(string corpo, string campo)
    {
        using HttpResponseMessage resposta = await EnviarAsync("formularios/INSCRICAO/itens", RaizAdmin, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))
            .Should().Contain((campo, RecusaDeCampoObrigatorioNoDominio.Code));
    }

    private async Task RecusarNoDominioAsync(string recurso, string corpo, string campo, string codigo)
    {
        using HttpResponseMessage resposta = await EnviarAsync(recurso, RaizAdmin, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString())).Should().Equal((campo, codigo));
    }

    private async Task<HttpResponseMessage> EnviarAsync(string recurso, string raiz, string corpo)
    {
        Guid processoId = await SemearAsync();
        using HttpRequestMessage request = CriarRequest($"{raiz}{processoId}/{recurso}", corpo);

        using HttpClient client = fixture.Factory.CreateClient();
        return await client.SendAsync(request);
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
