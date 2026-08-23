namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Desativa um motivo. O efeito é prospectivo (UNI-REQ-0122): ele deixa de
/// entrar em novas publicações e permanece onde já foi disponibilizado.
/// </summary>
public sealed record DesativarMotivoDecisaoIsencaoCommand(Guid Id) : ICommand<Result>;
