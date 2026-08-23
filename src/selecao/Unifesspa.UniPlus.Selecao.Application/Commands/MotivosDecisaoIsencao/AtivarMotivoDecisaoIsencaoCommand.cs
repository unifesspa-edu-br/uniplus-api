namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reativa um motivo, devolvendo-o às novas publicações.</summary>
public sealed record AtivarMotivoDecisaoIsencaoCommand(Guid Id) : ICommand<Result>;
