namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

public sealed record EditalPublicadoEvent(Guid EditalId, string NumeroEdital) : DomainEventBase;
