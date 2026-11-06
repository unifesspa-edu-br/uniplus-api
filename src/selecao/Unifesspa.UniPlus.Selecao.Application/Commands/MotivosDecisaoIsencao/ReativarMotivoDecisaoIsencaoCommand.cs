namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reativa um motivo, devolvendo-o às novas publicações.</summary>
public sealed record ReativarMotivoDecisaoIsencaoCommand(Guid Id) : ICommand<Result>;
