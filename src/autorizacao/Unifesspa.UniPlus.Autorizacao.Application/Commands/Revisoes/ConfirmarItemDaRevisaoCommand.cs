namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Confirma o item: a concessão segue ativa.</summary>
public sealed record ConfirmarItemDaRevisaoCommand(Guid RevisaoId, Guid ItemId) : ICommand<Result>;
