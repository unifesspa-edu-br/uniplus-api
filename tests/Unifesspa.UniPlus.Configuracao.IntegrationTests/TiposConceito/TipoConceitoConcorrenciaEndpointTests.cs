namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposConceito;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure.TiposConceitoHttpDeTeste;

/// <summary>
/// A desativação do tipo de conceito que perde a corrida de escrita responde 409 pelo tratamento central
/// de <c>DbUpdateConcurrencyException</c> (ADR-0119): o handler não captura a exceção, e a resposta é a
/// mesma de todo endpoint de escrita. A corrida é forçada pelo lock de linha de uma transação segurada.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TipoConceitoConcorrenciaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoConceitoConcorrenciaEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "DELETE que perde a corrida de escrita do mesmo tipo de conceito responde 409 pelo tratamento central")]
    public async Task Desativar_ColideComEscritaConcorrente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico());
        await using TravaDeLinha trava = await TravaDeLinha.TomarAsync(_fixture.Factory, id);

        Task<HttpResponseMessage> desativacao = DesativarAsync(client, id);
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(_fixture.Factory, desativacao, [trava.Pid]);
        await trava.ConfirmarAsync();

        using HttpResponseMessage resposta = await desativacao;
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument problema = await LerAsync(resposta);
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.concorrencia.conflito");
    }

    /// <summary>A transação com o <c>UPDATE</c> ainda não confirmado: quem escrever a mesma linha espera por ela.</summary>
    private sealed class TravaDeLinha : IAsyncDisposable
    {
        private readonly AsyncServiceScope _escopo;
        private readonly IDbContextTransaction _transacao;

        private TravaDeLinha(AsyncServiceScope escopo, IDbContextTransaction transacao, int pid)
        {
            _escopo = escopo;
            _transacao = transacao;
            Pid = pid;
        }

        public int Pid { get; }

        public static async Task<TravaDeLinha> TomarAsync(MonolitoApiFactory api, Guid id)
        {
            AsyncServiceScope escopo = api.Services.CreateAsyncScope();
            UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            IDbContextTransaction transacao = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE configuracao.tipos_conceito SET ativo = ativo WHERE id = {id}");
            return new TravaDeLinha(escopo, transacao, await ConcorrenciaTestHelpers.GetConnectionPidAsync(db));
        }

        public Task ConfirmarAsync() => _transacao.CommitAsync();

        public async ValueTask DisposeAsync()
        {
            await _transacao.DisposeAsync();
            await _escopo.DisposeAsync();
        }
    }
}
