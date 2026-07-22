namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

// Cenários produtivos do outbox cascading (ADR-0005), reconstruídos sobre o
// slice ProcessoSeletivo (Story #759, T4 #785) — o slice Edital legado que
// demonstrava isso foi removido por inteiro (#782).
//
// Os dois testes têm dupla marcação `Category=OutboxCapability` +
// `Category=OutboxCascading` — evita ambiguidade entre as duas suítes e
// mantém o filtro `OutboxCapability` correspondendo ao conjunto demonstrável
// de capacidades do outbox produtivo nesta fase.
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CascadingScenariosTests
{
    private readonly CascadingFixture _fixture;

    public CascadingScenariosTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    // V8 — caminho feliz: cascading entrega ProcessoPublicadoEvent ao handler
    // local da queue PG após Publicar+SaveChanges. Atomicidade write+evento
    // verificada indiretamente: se a transação não tivesse drenado o envelope
    // junto do SaveChanges, o listener da queue PG não receberia o evento.
    [Fact(DisplayName =
        "V8 — cascading entrega ProcessoPublicadoEvent ao handler local PG após Publicar+SaveChanges")]
    public async Task V8_Cascading_EntregaEvento_AposPublicarViaCommand()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);

        using HttpClient _ = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        collector.Clear();

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope seedScope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(seedDb, $"V8 cascading {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        var command = new PublicarProcessoSeletivoCommand(
            processoId,
            Numero: null,
            PeriodoInscricaoInicio: DateOnly.FromDateTime(DateTime.UtcNow),
            PeriodoInscricaoFim: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            DocumentoEditalId: documentoId,
            Ato: DadosDoAtoDeTeste.Padrao);

        await bus.InvokeAsync(command);

        ProcessoPublicadoEvent? evento = await EsperarEventoAsync(collector, processoId, TimeSpan.FromSeconds(15));

        evento.Should().NotBeNull(
            "cascading messages do handler devem ser persistidas pelo PersistOrSendAsync na transação ativa e entregues pelo listener da queue PG");
        evento!.ProcessoSeletivoId.Should().Be(processoId);
        evento.EditalId.Should().NotBeEmpty();

        // EditalId é o nome histórico do membro (contrato do envelope durável e do schema
        // Avro); o VALOR é o id do ato criador, e é por ele que a versão o referencia — por
        // valor, sem FK (ADR-0061). O ato em si vive em Publicações e chega lá pela fila.
        bool persistido = await db.VersoesConfiguracao.AsNoTracking()
            .AnyAsync(v => v.AtoCriadorId == evento.EditalId);
        persistido.Should().BeTrue();
    }

    // V9 — rollback: exceção pós-SaveChanges deve eliminar entidade e
    // envelope. O retorno cascading sequer chega a ser executado (throw
    // antes do return); a IEnvelopeTransaction faz o rollback no catch.
    [Fact(DisplayName =
        "V9 — rollback cascading: exceção pós-SaveChanges deixa entidade ausente e envelope sem registro")]
    public async Task V9_RollbackCascading_DeixaEntidadeEEnvelopeAusentes()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);

        using HttpClient _ = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        collector.Clear();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        string nomeProcesso = $"V9 cascading rollback {Guid.CreateVersion7()}";
        var command = new FalharAposPublicarCascadingCommand(nomeProcesso);

        Func<Task> act = () => bus.InvokeAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(FalharAposPublicarCascadingHandler.MensagemErro);

        bool entidadePersistida = await db.ProcessosSeletivos.AsNoTracking()
            .AnyAsync(p => p.Nome == nomeProcesso);
        entidadePersistida.Should().BeFalse(
            "rollback da transação Wolverine + EF deve eliminar o INSERT do processo (e do edital/snapshot dependentes) — "
            + "sem a transação ambiente, o SaveChanges já teria committado independente do throw seguinte");

        // O throw acontece ANTES do `return (Result, IEnumerable<object>)` —
        // CaptureCascadingMessages só roda em retorno normal do handler, então
        // nenhum envelope chega a ser criado para este ProcessoPublicadoEvent
        // por construção (não é preciso consultar wolverine_outgoing_envelopes:
        // não há caminho de código que o escreva neste cenário). A prova
        // observável de "nada vazou" é o coletor permanecer vazio.
        await Task.Delay(TimeSpan.FromSeconds(3));
        collector.Snapshot().Should().BeEmpty(
            "sem cascading emitido pelo handler que lançou exceção, o subscritor nunca é acionado");
    }

    internal static async Task<ProcessoPublicadoEvent?> EsperarEventoAsync(
        DomainEventCollector collector,
        Guid processoSeletivoIdEsperado,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            ProcessoPublicadoEvent? candidato = collector.Snapshot()
                .FirstOrDefault(e => e.ProcessoSeletivoId == processoSeletivoIdEsperado);

            if (candidato is not null)
            {
                return candidato;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }

        return null;
    }
}
