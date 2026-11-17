namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
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

    [Fact(DisplayName =
        "V8 — cascading entrega ProcessoPublicadoEvent ao handler local PG após Publicar+SaveChanges")]
    public async Task V8_Cascading_EntregaEvento_AposPublicarViaCommand()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient cliente = api.CreateClient();
        DomainEventCollector coletor = api.Services.GetRequiredService<DomainEventCollector>();
        coletor.Clear();
        (Guid processoId, Guid documentoId) = await PrepararProcessoPublicavelAsync(api);
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        await PublicarProcessoAsync(escopo, processoId, documentoId);
        ProcessoPublicadoEvent? evento = await EsperarEventoAsync(coletor, processoId, TimeSpan.FromSeconds(15));
        evento.Should().NotBeNull("cascading messages devem ser persistidas e entregues pelo listener da queue PG");
        evento!.ProcessoSeletivoId.Should().Be(processoId);
        evento.EditalId.Should().NotBeEmpty();
        await VerificarVersaoDaPublicacaoAsync(escopo, evento.EditalId);
    }

    [Fact(DisplayName =
        "V9 — rollback cascading: exceção pós-SaveChanges deixa entidade ausente e envelope sem registro")]
    public async Task V9_RollbackCascading_DeixaEntidadeEEnvelopeAusentes()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient cliente = api.CreateClient();
        DomainEventCollector coletor = api.Services.GetRequiredService<DomainEventCollector>();
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        string nomeProcesso = $"V9 cascading rollback {Guid.CreateVersion7()}";
        Guid processoId = Guid.CreateVersion7();
        await PublicarComFalhaAsync(escopo, nomeProcesso, processoId);
        await VerificarProcessoAusenteAsync(escopo, nomeProcesso);
        await Task.Delay(TimeSpan.FromSeconds(3));
        coletor.Snapshot().Where(e => e.ProcessoSeletivoId == processoId).Should().BeEmpty(
            "o rollback impede que eventos deste processo sejam entregues ao coletor");
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> PrepararProcessoPublicavelAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"V8 cascading {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    private static async Task PublicarProcessoAsync(AsyncServiceScope escopo, Guid processoId, Guid documentoId)
    {
        IMessageBus bus = escopo.ServiceProvider.GetRequiredService<IMessageBus>();
        var comando = new PublicarProcessoSeletivoCommand(
            processoId, Numero: null, PeriodoInscricaoInicio: null, PeriodoInscricaoFim: null,
            DocumentoEditalId: documentoId, Ato: DadosDoAtoDeTeste.Padrao);
        await bus.InvokeAsync(comando);
    }

    private static async Task VerificarVersaoDaPublicacaoAsync(AsyncServiceScope escopo, Guid atoCriadorId)
    {
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        bool persistido = await db.VersoesConfiguracao.AsNoTracking().AnyAsync(v => v.AtoCriadorId == atoCriadorId);
        persistido.Should().BeTrue();
    }

    private static async Task PublicarComFalhaAsync(AsyncServiceScope escopo, string nomeProcesso, Guid processoId)
    {
        IMessageBus bus = escopo.ServiceProvider.GetRequiredService<IMessageBus>();
        var comando = new FalharAposPublicarCascadingCommand(nomeProcesso, processoId);
        Func<Task> acao = () => bus.InvokeAsync(comando);
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(FalharAposPublicarCascadingHandler.MensagemErro);
    }

    private static async Task VerificarProcessoAusenteAsync(AsyncServiceScope escopo, string nomeProcesso)
    {
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        bool persistido = await db.ProcessosSeletivos.AsNoTracking().AnyAsync(p => p.Nome == nomeProcesso);
        persistido.Should().BeFalse("o rollback da transação deve eliminar o processo e suas entidades dependentes");
    }

    internal static async Task<ProcessoPublicadoEvent?> EsperarEventoAsync(
        DomainEventCollector collector, Guid processoSeletivoIdEsperado, TimeSpan timeout)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < limite)
        {
            ProcessoPublicadoEvent? evento = collector.Snapshot()
                .FirstOrDefault(e => e.ProcessoSeletivoId == processoSeletivoIdEsperado);
            if (evento is not null)
            {
                return evento;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }
        return null;
    }
}
