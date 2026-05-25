namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;

/// <summary>Comando do módulo CRUD (EF Core, store 'main').</summary>
public sealed record CriarRegistroCrud(Guid Id, string Descricao);

/// <summary>Evento de integração emitido pelo handler EF Core (outbox 'main').</summary>
public sealed record RegistroCrudCriado(Guid Id);

/// <summary>
/// Fato anexado a um stream no store ancillary (Marten). No teste é anexado
/// diretamente pela sessão do store ancillary, provando que o event store coabita e
/// é utilizável no mesmo host do outbox EF Core.
/// </summary>
public sealed record FatoEsAnexado(Guid StreamId, string Dado, DateTimeOffset OcorridoEm);
