namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.SharedKernel.Domain.Events;

public sealed record ClassificacaoPublicadaEvent(Guid EditalId, string NumeroEdital) : DomainEventBase;
