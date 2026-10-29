namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A recusa da criação do processo não prende a chave de idempotência. A permissão é decidida
/// no handler, contra a unidade do corpo, portanto depois de a reserva existir: a recusa passa
/// pelo filtro de idempotência e, se ficasse guardada, o retry com a mesma chave depois da
/// concessão receberia a recusa antiga em replay.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CriacaoDoProcessoLiberaAChaveEndpointTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/processos-seletivos";
    private const string Leandro = "leandro.viana";

    private readonly CascadingFixture _fixture;

    public CriacaoDoProcessoLiberaAChaveEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() =>
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Recusada a criação por falta de permissão na unidade, a mesma chave cria depois da concessão")]
    public async Task Criar_AposConcessaoNaUnidadeComAMesmaChave_Cria()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync();
        using HttpClient client = ClienteDe(semJti: false);
        string chave = Guid.CreateVersion7().ToString("N");
        string corpo = Corpo(tipoProcessoId, unidadeId);

        using HttpResponseMessage recusada = await PostAsync(client, corpo, chave);
        recusada.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(
            _fixture.Factory.Services, Leandro, perfil, EscopoDaConcessao.DaUnidade(unidadeId));

        using HttpResponseMessage criada = await PostAsync(client, corpo, chave);

        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
        criada.Headers.Contains("Idempotency-Replayed").Should().BeFalse("a recusa não deixou resposta a reproduzir");
    }

    [Fact(DisplayName = "O 401 de identidade incompleta na criação não é guardado: a mesma chave, com a identidade completa, cria")]
    public async Task Criar_AposIdentidadeIncompletaComAMesmaChave_Cria()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync();
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(
            _fixture.Factory.Services, Leandro, perfil, EscopoDaConcessao.DaUnidade(unidadeId));
        string chave = Guid.CreateVersion7().ToString("N");
        string corpo = Corpo(tipoProcessoId, unidadeId);

        using (HttpClient semJti = ClienteDe(semJti: true))
        {
            using HttpResponseMessage desafio = await PostAsync(semJti, corpo, chave);
            desafio.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using HttpClient completo = ClienteDe(semJti: false);
        using HttpResponseMessage criada = await PostAsync(completo, corpo, chave);

        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
    }

    private static string Corpo(Guid tipoProcessoId, Guid unidadeId) => $$"""
        {
          "nome": "PS libera chave {{Guid.CreateVersion7():N}}",
          "tipoProcessoOrigemId": "{{tipoProcessoId}}",
          "origemCandidatos": "inscricaoPropria",
          "unidadeAdministradoraOrigemId": "{{unidadeId}}",
          "localidadeCodigoIbge": "1504208",
          "localidadeNome": "Marabá",
          "localidadeUf": "PA",
          "identificadorLegivel": "{{IdentificadoresDeTeste.NovoValor()}}"
        }
        """;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string corpo, string chave)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", chave);
        return await client.SendAsync(request);
    }

    // O token só identifica a pessoa; sem papel algum, quem lhe dá a permissão é a concessão gravada.
    private HttpClient ClienteDe(bool semJti)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Leandro);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Admin Test");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@unifesspa.edu.br");
        if (semJti)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.SemJtiHeader, "1");
        }

        return client;
    }

    private async Task<(Guid UnidadeId, Guid TipoProcessoId)> SemearAsync()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        Guid unidadeId;
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            OrganizacaoInstitucionalDbContext org = scope.ServiceProvider
                .GetRequiredService<OrganizacaoInstitucionalDbContext>();
            Unidade unidade = Unidade.Criar(
                nome: "Centro de Processos Seletivos — chave",
                alias: null,
                slug: $"ceps-chave-{sufixo}",
                sigla: $"CEPSC{sufixo}",
                codigo: $"CEPSC-{sufixo}",
                unidadeSuperiorId: null,
                tipo: TipoUnidade.Centro,
                unidadeAcademica: true,
                vigenciaInicio: new DateOnly(2020, 1, 1),
                vigenciaFim: null,
                cidadeCodigoIbge: "1504208",
                cidadeNome: "Marabá",
                cidadeUf: "PA").Value!;
            org.Unidades.Add(unidade);
            await org.SaveChangesAsync();
            unidadeId = unidade.Id;
        }

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext config = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        Guid tipoProcessoId = await config.TiposProcesso
            .Where(tipo => tipo.Codigo == "SiSU" && tipo.Ativo)
            .Select(tipo => tipo.Id)
            .SingleAsync();
        return (unidadeId, tipoProcessoId);
    }
}
