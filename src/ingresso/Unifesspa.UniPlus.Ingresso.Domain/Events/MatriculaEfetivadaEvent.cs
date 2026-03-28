namespace Unifesspa.UniPlus.Ingresso.Domain.Events;

using Unifesspa.UniPlus.SharedKernel.Domain.Events;

public sealed record MatriculaEfetivadaEvent(Guid MatriculaId, Guid CandidatoId, string CodigoCurso) : DomainEventBase;
