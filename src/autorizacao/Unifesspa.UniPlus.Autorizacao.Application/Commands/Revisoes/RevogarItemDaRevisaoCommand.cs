namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Revoga a concessão do item, com efeito imediato.</summary>
public sealed record RevogarItemDaRevisaoCommand(Guid RevisaoId, Guid ItemId, string? Motivo) : ICommand<Result>;
