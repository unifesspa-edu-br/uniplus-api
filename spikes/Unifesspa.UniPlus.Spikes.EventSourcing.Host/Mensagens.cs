namespace Unifesspa.UniPlus.Spikes.EventSourcing.Host;

/// <summary>Marcador do assembly do host (discovery do Wolverine + localização do DLL).</summary>
public sealed class HostMarker;

/// <summary>Abre um edital no event store ancillary (Marten).</summary>
public sealed record AbrirEditalEs(Guid Id, string Numero, string Titulo);

/// <summary>Retifica um edital no event store ancillary (concorrência otimista por stream).</summary>
public sealed record RetificarEditalEs(Guid Id, string Motivo);
