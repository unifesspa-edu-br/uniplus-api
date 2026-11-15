namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using static CenarioDeEsperaPelaTrava;

/// <summary>
/// A espera pela trava do processo tem limite: com outra operação retendo a linha, a publicação do ato avulso responde 503,
/// em vez de esperar o tempo do driver e falhar sem explicação. A recusa fica guardada pela chave de idempotência, porque
/// depois da trava o ato é publicado na divulgação corrente do processo, que o vencedor da corrida pode ter criado: o
/// cliente relê o estado e envia com uma nova chave. O limite vale só enquanto se espera a linha do processo.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class EsperaPelaTravaDoProcessoEndpointTests : IAsyncLifetime
{
    private readonly CenarioDeEsperaPelaTrava _cenario;

    public EsperaPelaTravaDoProcessoEndpointTests(CascadingFixture fixture)
    {
        _cenario = CenarioDeEsperaPelaTrava.Criar(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _cenario.DisposeAsync();

    [Fact(DisplayName = "O ato avulso que espera a trava além do limite responde 503 e guarda a recusa: a mesma chave devolve a mesma recusa, e uma nova chave publica uma só vez")]
    public async Task AtoAvulsoSobTravaRetida_GuardaARecusa_ENovaChaveExecutaUmaVez()
    {
        PedidoDeAtoAvulso pedido = await PedidoDeAtoAvulso.PrepararAsync(_cenario);
        await using ConexaoQueTravaOProcesso outraOperacao = await ConexaoQueTravaOProcesso.TravarAsync(_cenario.Services, pedido.ProcessoId);

        await ConferirRecusaSemGravarAsync(pedido);
        await outraOperacao.DesfazerAsync();

        await ConferirQueAMesmaChaveRecebeARecusaGuardadaAsync(pedido);
        await ConferirQueUmaNovaChavePublicaUmaSoVezAsync(pedido);
    }

    [Fact(DisplayName = "A trava do processo retida além do limite lança a espera esgotada, depois de esperar o limite configurado")]
    public async Task TravarAsync_TravaRetida_LancaEsperaEsgotadaDepoisDoLimite()
    {
        Guid processoId = await _cenario.SemearProcessoAsync(nameof(TravarAsync_TravaRetida_LancaEsperaEsgotadaDepoisDoLimite));
        await using ConexaoQueTravaOProcesso outraOperacao = await ConexaoQueTravaOProcesso.TravarAsync(_cenario.Services, processoId);
        await using TransacaoDoRepositorio transacao = await TransacaoDoRepositorio.IniciarAsync(_cenario.Services);
        Stopwatch espera = Stopwatch.StartNew();

        Func<Task> travar = () => transacao.Processos.TravarAsync(processoId);

        EsperaPelaTravaEsgotadaException falha = (await travar.Should().ThrowAsync<EsperaPelaTravaEsgotadaException>()).Which;
        espera.Elapsed.Should().BeGreaterThanOrEqualTo(LimiteDaEspera - TimeSpan.FromMilliseconds(50));
        falha.InnerException.Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(PostgresErrorCodes.LockNotAvailable);
    }

    [Fact(DisplayName = "Depois de obter a trava do processo, a espera por outra linha na mesma transação passa do limite sem falhar")]
    public async Task TravaSeguinteNaMesmaTransacao_EsperaAlemDoLimiteSemFalhar()
    {
        Guid processoId = await _cenario.SemearProcessoAsync(nameof(TravaSeguinteNaMesmaTransacao_EsperaAlemDoLimiteSemFalhar) + "-A");
        Guid outroProcessoId = await _cenario.SemearProcessoAsync(nameof(TravaSeguinteNaMesmaTransacao_EsperaAlemDoLimiteSemFalhar) + "-B");
        await using ConexaoQueTravaOProcesso outraOperacao = await ConexaoQueTravaOProcesso.TravarAsync(_cenario.Services, outroProcessoId);
        await using TransacaoDoRepositorio transacao = await TransacaoDoRepositorio.IniciarAsync(_cenario.Services);
        await transacao.Processos.TravarAsync(processoId);

        Task seguinte = transacao.TravarLinhaDiretoAsync(outroProcessoId);
        await Task.Delay(LimiteDaEspera * 3);
        seguinte.IsCompleted.Should().BeFalse("o limite da espera foi devolvido a zero depois da trava do processo");
        await outraOperacao.ConfirmarAsync();

        await seguinte.WaitAsync(EsperaDoTeste);
    }

    [Fact(DisplayName = "Depois de obter a trava do processo, o limite de espera que a transação já tinha é devolvido")]
    public async Task TravaDoProcesso_DevolveOLimiteQueATransacaoJaTinha()
    {
        Guid processoId = await _cenario.SemearProcessoAsync(nameof(TravaDoProcesso_DevolveOLimiteQueATransacaoJaTinha));
        await using TransacaoDoRepositorio transacao = await TransacaoDoRepositorio.IniciarAsync(_cenario.Services);
        await transacao.DefinirLimiteDaEsperaAsync("5s");

        await transacao.Processos.TravarAsync(processoId);

        (await transacao.LimiteDaEsperaAsync()).Should().Be("5s");
    }

    private static async Task ConferirRecusaSemGravarAsync(PedidoDeAtoAvulso pedido)
    {
        using HttpResponseMessage resposta = await pedido.EnviarAsync(pedido.Chave).WaitAsync(EsperaDoTeste);
        await ConferirEsperaEsgotadaGuardadaAsync(resposta);
        (await pedido.AtosPublicadosAsync()).Should().Be(0, "a transação da publicação foi desfeita");
    }

    private static async Task ConferirQueAMesmaChaveRecebeARecusaGuardadaAsync(PedidoDeAtoAvulso pedido)
    {
        using HttpResponseMessage repeticao = await pedido.EnviarAsync(pedido.Chave).WaitAsync(EsperaDoTeste);

        await ConferirEsperaEsgotadaGuardadaAsync(repeticao);
        repeticao.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle().Which.Should().Be("true");
        (await pedido.AtosPublicadosAsync()).Should().Be(0, "a repetição com a mesma chave devolveu a recusa guardada e não executou");
    }

    private static async Task ConferirQueUmaNovaChavePublicaUmaSoVezAsync(PedidoDeAtoAvulso pedido)
    {
        string novaChave = Guid.CreateVersion7().ToString("N");
        Guid atoId;
        using (HttpResponseMessage publicada = await pedido.EnviarAsync(novaChave).WaitAsync(EsperaDoTeste))
        {
            publicada.StatusCode.Should().Be(HttpStatusCode.Created, await publicada.Content.ReadAsStringAsync());
            atoId = (await publicada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        }

        using HttpResponseMessage reenvio = await pedido.EnviarAsync(novaChave).WaitAsync(EsperaDoTeste);
        reenvio.StatusCode.Should().Be(HttpStatusCode.Created);
        (await reenvio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid().Should().Be(atoId);
        (await pedido.AtosPublicadosAsync()).Should().Be(1, "a repetição fiel responde pelo que foi guardado e não executa de novo");
    }

    /// <summary>O ato avulso de um processo já divulgado, enviado com o mesmo corpo e a chave de idempotência indicada.</summary>
    private sealed class PedidoDeAtoAvulso
    {
        private readonly CenarioDeEsperaPelaTrava _cenario;
        private readonly Guid _documentoId;
        private PedidoDeAtoAvulso(CenarioDeEsperaPelaTrava cenario, Guid processoId, Guid documentoId)
        {
            _cenario = cenario;
            ProcessoId = processoId;
            _documentoId = documentoId;
        }

        internal Guid ProcessoId { get; }

        internal string Chave { get; } = Guid.CreateVersion7().ToString("N");

        internal static async Task<PedidoDeAtoAvulso> PrepararAsync(CenarioDeEsperaPelaTrava cenario)
        {
            await TiposDeAtoSeeder.SemearAsync(cenario.Preparo.Services);
            using HttpClient cliente = cenario.Preparo.CreateClient();
            (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(cenario.Preparo, cliente);
            Guid documentoId = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(
                cenario.Preparo, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");
            return new PedidoDeAtoAvulso(cenario, processoId, documentoId);
        }

        internal async Task<HttpResponseMessage> EnviarAsync(string chave)
        {
            using HttpClient cliente = _cenario.Host.CreateClient();
            return await PublicarAtoAvulsoEndpointTests.PublicarAvisoAsync(cliente, ProcessoId, _documentoId, chave);
        }

        internal async Task<int> AtosPublicadosAsync()
        {
            await using AsyncServiceScope escopo = _cenario.Preparo.Services.CreateAsyncScope();
            UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            return await db.AtosDoProcesso.AsNoTracking()
                .CountAsync(a => a.ProcessoSeletivoId == ProcessoId && a.TipoCodigo == DadosDoAtoDeTeste.TipoAviso);
        }
    }

    private sealed class TransacaoDoRepositorio : IAsyncDisposable
    {
        private readonly AsyncServiceScope _escopo;
        private readonly UniPlusDbContext _db;
        private readonly IDbContextTransaction _transacao;

        private TransacaoDoRepositorio(AsyncServiceScope escopo, UniPlusDbContext db, IDbContextTransaction transacao)
        {
            _escopo = escopo;
            _db = db;
            _transacao = transacao;
            Processos = ProcessoSeletivoRepositoryDeTeste.Criar(db, escopo.ServiceProvider.GetRequiredService<IOptions<TravaDoProcessoOptions>>());
        }

        internal ProcessoSeletivoRepository Processos { get; }

        internal static async Task<TransacaoDoRepositorio> IniciarAsync(IServiceProvider servicos)
        {
            AsyncServiceScope escopo = servicos.CreateAsyncScope();
            UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            return new TransacaoDoRepositorio(escopo, db, await db.Database.BeginTransactionAsync());
        }

        /// <summary>Trava a linha sem passar pelo repositório, como qualquer outro ponto que espera por uma trava.</summary>
        internal Task<int> TravarLinhaDiretoAsync(Guid processoId) =>
            _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM selecao.processos_seletivos WHERE id = {processoId} FOR UPDATE");

        internal Task<int> DefinirLimiteDaEsperaAsync(string limite) =>
            _db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('lock_timeout', {limite}, true)");

        internal async Task<string> LimiteDaEsperaAsync() =>
            (await _db.Database.SqlQuery<string>($"SELECT current_setting('lock_timeout') AS \"Value\"").ToListAsync()).Single();

        public async ValueTask DisposeAsync()
        {
            await _transacao.DisposeAsync();
            await _escopo.DisposeAsync();
        }
    }
}
