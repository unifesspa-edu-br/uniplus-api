namespace Unifesspa.UniPlus.Ingresso.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

public sealed record MatriculaEfetivadaEvent(Guid MatriculaId, Guid CandidatoId, string CodigoCurso) : DomainEventBase;
