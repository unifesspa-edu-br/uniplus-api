namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

public sealed record InscricaoRealizadaEvent(Guid InscricaoId, Guid CandidatoId, Guid EditalId) : DomainEventBase;
