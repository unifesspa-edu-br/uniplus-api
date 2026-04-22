namespace Unifesspa.UniPlus.Ingresso.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

public sealed record CandidatoConvocadoEvent(Guid InscricaoId, Guid CandidatoId, Guid ChamadaId, string Protocolo) : DomainEventBase;
