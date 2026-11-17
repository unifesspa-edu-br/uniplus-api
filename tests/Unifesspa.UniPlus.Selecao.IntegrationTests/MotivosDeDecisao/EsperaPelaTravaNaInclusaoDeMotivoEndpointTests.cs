namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using static Outbox.Cascading.CenarioDeEsperaPelaTrava;

/// <summary>
/// A inclusão do motivo no processo com sessão de retificação aceita <c>If-Match: *</c>, que casa com qualquer sessão: se a
/// requisição perde a espera pela trava e a repetição com a mesma chave executasse, ela poderia atingir uma sessão aberta
/// depois, que ninguém revisou. Por isso a recusa por espera esgotada fica guardada pela chave, sem Retry-After: a mesma
/// chave devolve a mesma recusa, e só uma nova chave executa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class EsperaPelaTravaNaInclusaoDeMotivoEndpointTests : IAsyncLifetime
{
    private readonly CenarioDeEsperaPelaTrava _cenario;

    public EsperaPelaTravaNaInclusaoDeMotivoEndpointTests(CascadingFixture fixture)
    {
        _cenario = CenarioDeEsperaPelaTrava.Criar(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _cenario.Preparo.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_do_processo WHERE codigo LIKE 'TEST\\_EPT\\_%' ESCAPE '\\'");
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE 'TEST\\_EPT\\_%' ESCAPE '\\'");
        await _cenario.DisposeAsync();
    }

    [Fact(DisplayName = "A inclusão de motivo que perde a espera pela trava responde 503 sem Retry-After e guarda a recusa: a mesma chave devolve a mesma recusa, e uma nova chave inclui uma só vez")]
    public async Task Inclusao_SobTravaRetida_GuardaARecusa_ENovaChaveExecutaUmaVez()
    {
        PedidoDeInclusao pedido = await PedidoDeInclusao.PrepararAsync(_cenario);

        await RecusarEnquantoOutraOperacaoRetemOProcessoAsync(pedido);

        await ConferirQueAMesmaChaveRecebeARecusaGuardadaAsync(pedido);
        await ConferirQueUmaNovaChaveIncluiUmaSoVezAsync(pedido);
    }

    private async Task RecusarEnquantoOutraOperacaoRetemOProcessoAsync(PedidoDeInclusao pedido)
    {
        await using ConexaoQueTravaOProcesso outraOperacao = await ConexaoQueTravaOProcesso.TravarAsync(_cenario.Services, pedido.ProcessoId);

        using HttpResponseMessage recusada = await pedido.EnviarAsync(pedido.Chave).WaitAsync(EsperaDoTeste);

        await ConferirEsperaEsgotadaGuardadaAsync(recusada);
        (await pedido.EscolhasDoProcessoAsync()).Should().Be(0, "a transação da inclusão foi desfeita");
        await outraOperacao.DesfazerAsync();
    }

    private static async Task ConferirQueAMesmaChaveRecebeARecusaGuardadaAsync(PedidoDeInclusao pedido)
    {
        using HttpResponseMessage repeticao = await pedido.EnviarAsync(pedido.Chave).WaitAsync(EsperaDoTeste);

        await ConferirEsperaEsgotadaGuardadaAsync(repeticao);
        repeticao.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle().Which.Should().Be("true");
        (await pedido.EscolhasDoProcessoAsync()).Should().Be(0, "a repetição com a mesma chave devolveu a recusa guardada e não executou");
    }

    private static async Task ConferirQueUmaNovaChaveIncluiUmaSoVezAsync(PedidoDeInclusao pedido)
    {
        using HttpResponseMessage novaChave = await pedido.EnviarAsync(Guid.CreateVersion7().ToString("N")).WaitAsync(EsperaDoTeste);

        novaChave.StatusCode.Should().Be(HttpStatusCode.NoContent, await novaChave.Content.ReadAsStringAsync());
        (await pedido.EscolhasDoProcessoAsync()).Should().Be(1, "a nova chave inclui o motivo no estado que o cliente releu");
    }

    private sealed class PedidoDeInclusao
    {
        private readonly CenarioDeEsperaPelaTrava _cenario;
        private readonly string _rota;

        private PedidoDeInclusao(CenarioDeEsperaPelaTrava cenario, Guid processoId, string rota)
        {
            _cenario = cenario;
            ProcessoId = processoId;
            _rota = rota;
        }

        internal Guid ProcessoId { get; }

        internal string Chave { get; } = Guid.CreateVersion7().ToString("N");

        internal static async Task<PedidoDeInclusao> PrepararAsync(CenarioDeEsperaPelaTrava cenario)
        {
            Guid processoId = await cenario.SemearProcessoAsync(nameof(EsperaPelaTravaNaInclusaoDeMotivoEndpointTests));
            Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(cenario.Preparo.Services, processoId);
            return new PedidoDeInclusao(cenario, processoId, LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, await SemearMotivoAsync(cenario)));
        }

        internal async Task<HttpResponseMessage> EnviarAsync(string chave)
        {
            using HttpClient client = _cenario.Host.CreateClient();
            using HttpRequestMessage request = new(HttpMethod.Put, new Uri(_rota, UriKind.Relative))
            {
                Content = JsonContent.Create(new { justificativa = (string?)null }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", chave);
            return await client.SendAsync(request);
        }

        internal async Task<int> EscolhasDoProcessoAsync()
        {
            await using AsyncServiceScope escopo = _cenario.Preparo.Services.CreateAsyncScope();
            UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            return await db.MotivosDoProcesso.AsNoTracking().CountAsync(m => m.ProcessoSeletivoId == ProcessoId);
        }

        private static async Task<Guid> SemearMotivoAsync(CenarioDeEsperaPelaTrava cenario)
        {
            MotivoDeDecisao motivo = MotivoDeDecisao.Criar(new CamposDoMotivo(
                TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes,
                $"TEST_EPT_{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
                "Sem resultado no retorno do INEP para a edição indicada",
                null,
                SentidoDoMotivo.Desfavoravel)).Value!;
            await using AsyncServiceScope escopo = cenario.Preparo.Services.CreateAsyncScope();
            UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            db.MotivosDeDecisao.Add(motivo);
            await db.SaveChangesAsync();
            return motivo.Id;
        }
    }
}
