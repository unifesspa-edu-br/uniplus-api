namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Revoga uma concessão de acesso, com efeito imediato e sem aprovação.</summary>
public sealed record RevogarConcessaoCommand(Guid Id, string? Motivo) : ICommand<Result>;
