namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) um tipo de banca pelo seu <c>Id</c>.</summary>
public sealed record RemoverTipoBancaCommand(Guid Id) : ICommand<Result>;
