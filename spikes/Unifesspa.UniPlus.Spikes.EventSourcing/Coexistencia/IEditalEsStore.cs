using Marten;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;

/// <summary>
/// Marcador do <b>store ancillary</b> do Marten (event store dos agregados ES),
/// registrado via <c>AddMartenStore&lt;IEditalEsStore&gt;</c>. Por ser um store
/// ancillary, defere o papel de message store 'main' ao outbox EF Core/Postgres —
/// resolvendo o requisito do Wolverine de "exatamente um store main".
/// </summary>
public interface IEditalEsStore : IDocumentStore;
