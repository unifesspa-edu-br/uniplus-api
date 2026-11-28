namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
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
    private const string Leandro = ApoioDaCriacaoDoProcesso.Leandro;

    private readonly CascadingFixture _fixture;

    public CriacaoDoProcessoLiberaAChaveEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() =>
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Recusada a criação por falta de permissão na unidade, a mesma chave cria depois da concessão")]
    public async Task Criar_AposConcessaoNaUnidadeComAMesmaChave_Cria()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await ApoioDaCriacaoDoProcesso.SemearAsync(_fixture);
        using HttpClient client = ApoioDaCriacaoDoProcesso.ClienteDe(_fixture);
        string chave = Guid.CreateVersion7().ToString("N");
        string corpo = ApoioDaCriacaoDoProcesso.Corpo(tipoProcessoId, unidadeId, "PS libera chave");

        using HttpResponseMessage recusada = await ApoioDaCriacaoDoProcesso.PostAsync(client, corpo, chave);
        recusada.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await ApoioDaCriacaoDoProcesso.ConcederPermissaoNaUnidadeAsync(_fixture, unidadeId);

        using HttpResponseMessage criada = await ApoioDaCriacaoDoProcesso.PostAsync(client, corpo, chave);

        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
        criada.Headers.Contains("Idempotency-Replayed").Should().BeFalse("a recusa não deixou resposta a reproduzir");
    }

    [Fact(DisplayName = "O 401 de identidade incompleta na criação não é guardado: a mesma chave, com a identidade completa, cria")]
    public async Task Criar_AposIdentidadeIncompletaComAMesmaChave_Cria()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await ApoioDaCriacaoDoProcesso.SemearAsync(_fixture);
        await ApoioDaCriacaoDoProcesso.ConcederPermissaoNaUnidadeAsync(_fixture, unidadeId);
        string chave = Guid.CreateVersion7().ToString("N");
        string corpo = ApoioDaCriacaoDoProcesso.Corpo(tipoProcessoId, unidadeId, "PS libera chave");

        await VerificarRecusaDaIdentidadeIncompletaAsync(corpo, chave);

        using HttpClient completo = ApoioDaCriacaoDoProcesso.ClienteDe(_fixture);
        using HttpResponseMessage criada = await ApoioDaCriacaoDoProcesso.PostAsync(completo, corpo, chave);

        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
    }

    private async Task VerificarRecusaDaIdentidadeIncompletaAsync(string corpo, string chave)
    {
        using HttpClient semJti = ApoioDaCriacaoDoProcesso.ClienteDe(_fixture, semJti: true);
        using HttpResponseMessage desafio = await ApoioDaCriacaoDoProcesso.PostAsync(semJti, corpo, chave);
        desafio.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
