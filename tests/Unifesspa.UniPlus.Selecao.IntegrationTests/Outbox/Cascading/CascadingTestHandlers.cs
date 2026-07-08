namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Application.Abstractions;
using Domain.Entities;
using Domain.Events;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Handler do cenário de rollback (V9): semeia um processo conforme, publica
// de verdade (mesma orquestração de ProcessoSeletivo.Publicar do handler
// produtivo) e força uma exceção logo após o SaveChanges — nunca chega a
// retornar o IEnumerable<object> de cascading. O codegen do Wolverine
// reconhece IEnumerable<object> no retorno normal e instala
// CaptureCascadingMessages no postprocessor; como a chain está dentro de
// EnrollDbContextInTransaction, o throw pós-SaveChanges reverte também os
// INSERTs já executados na mesma transação ambiente (ADR-0005).
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class FalharAposPublicarCascadingHandler
{
    public const string MensagemErro = "Cenário V9 — exceção forçada após SaveChanges no caminho cascading";

    public static async Task<IEnumerable<object>> Handle(
        FalharAposPublicarCascadingCommand command,
        SelecaoDbContext db,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, command.NomeProcesso)
            .ConfigureAwait(false);

        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            numero: null,
            periodoInscricaoInicio: DateOnly.FromDateTime(DateTime.UtcNow),
            periodoInscricaoFim: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            documentoEditalId: documento.Id);

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(processo, dadosResult.Value!, documento.HashSha256!);

        Result<PublicacaoResultado> publicarResult = processo.Publicar(
            dadosResult.Value!,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "cascading-v9-test",
            timeProvider);

        db.SnapshotsPublicacao.Add(publicarResult.Value!.Snapshot);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        throw new InvalidOperationException(MensagemErro);
    }
}

// Subscritor in-memory de ProcessoPublicadoEvent — drena pela queue PG
// "domain-events" registrada na configuração produtiva e grava no coletor.
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class ProcessoPublicadoSubscriberHandler
{
    public static void Handle(
        ProcessoPublicadoEvent @event,
        DomainEventCollector collector)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(collector);

        collector.Record(@event);
    }
}
