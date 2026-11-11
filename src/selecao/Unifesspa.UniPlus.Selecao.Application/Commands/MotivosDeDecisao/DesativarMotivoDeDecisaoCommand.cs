namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Desativa um motivo. O efeito é prospectivo (UNI-REQ-0122): ele deixa de entrar em novas
/// publicações e continua utilizável no processo publicado que já o escolheu.
/// </summary>
public sealed record DesativarMotivoDeDecisaoCommand(Guid Id) : ICommand<Result>;
