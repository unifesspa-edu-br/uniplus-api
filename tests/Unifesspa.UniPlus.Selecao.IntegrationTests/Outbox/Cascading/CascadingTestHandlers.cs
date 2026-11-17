namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Application.Abstractions;

using Domain.Entities;
using Domain.Events;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        UniPlusDbContext db,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, command.NomeProcesso)
            .ConfigureAwait(false);

        db.VersoesConfiguracao.Add(Publicar(processo, documento, canonicalizer, timeProvider).Value!);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        throw new InvalidOperationException(MensagemErro);
    }

    private static DadosEdital DadosDoEdital(DocumentoAPublicar documento) => DadosEdital.Criar(
        numero: null,
        periodoInscricaoInicio: DateTimeOffset.UtcNow,
        periodoInscricaoFim: DateTimeOffset.UtcNow.AddDays(30),
        documentoEditalId: documento.Id).Value!;

    private static Result<VersaoConfiguracao> Publicar(
        ProcessoSeletivo processo, DocumentoAPublicar documento, ISnapshotPublicacaoCanonicalizer canonicalizer, TimeProvider timeProvider)
    {
        DadosEdital dados = DadosDoEdital(documento);

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, documento.HashSha256!, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

        return processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, documento.HashSha256!), AtorUsuarioContaId = "cascading-v9-test", Relogio = timeProvider, Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
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
