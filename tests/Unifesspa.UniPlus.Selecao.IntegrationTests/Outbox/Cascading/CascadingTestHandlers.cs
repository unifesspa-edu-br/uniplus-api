namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Domain.Entities;
using Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Handler do caminho feliz: cria/publica um Edital, faz SaveChanges e
// retorna a coleção de domain events do agregado como cascading messages.
// O codegen do Wolverine reconhece IEnumerable<object> no retorno e instala
// CaptureCascadingMessages no postprocessor; cada elemento entra em
// MessageContext.EnqueueCascadingAsync. Como a chain está dentro de
// EnrollDbContextInTransaction, Transaction != null e os envelopes são
// gravados em wolverine_outgoing_envelopes na MESMA transação EF —
// atomicidade write+evento (ADR-0005).
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class PublicarEditalCascadingHandler
{
    public static async Task<IEnumerable<object>> Handle(
        PublicarEditalCascadingCommand command,
        SelecaoDbContext db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(db);

        Edital edital = Edital.Criar(command.Numero, command.Titulo);
        edital.Publicar();
        db.Editais.Add(edital);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Cast<object> garante o switch case `IEnumerable<object>` em
        // MessageContext.EnqueueCascadingAsync sem depender de covariância
        // implícita do IDomainEvent (interface) para object.
        return edital.DequeueDomainEvents().Cast<object>();
    }
}

// Handler do rollback: cria/publica/salva e imediatamente lança exceção,
// simulando falha pós-SaveChanges. AC esperado: rollback de entidade +
// nenhum envelope persistido (atomicidade preservada pela transação que
// envolve SaveChanges + persistência do envelope).
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class FalharAposSaveChangesCascadingHandler
{
    public const string MensagemErro = "Cenário V9 — exceção forçada após SaveChanges no caminho cascading";

    public static async Task<IEnumerable<object>> Handle(
        FalharAposSaveChangesCascadingCommand command,
        SelecaoDbContext db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(db);

        Edital edital = Edital.Criar(command.Numero, command.Titulo);
        edital.Publicar();
        db.Editais.Add(edital);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        throw new InvalidOperationException(MensagemErro);
    }
}

// Subscritor in-memory de EditalPublicadoEvent — drena pela queue PG
// "domain-events" registrada na configuração produtiva e grava no coletor.
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class EditalPublicadoSubscriberHandler
{
    public static void Handle(
        EditalPublicadoEvent @event,
        DomainEventCollector collector)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(collector);

        collector.Record(@event);
    }
}
