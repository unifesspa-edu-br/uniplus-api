namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) um tipo de deficiência pelo seu <c>Id</c>.</summary>
public sealed record RemoverTipoDeficienciaCommand(Guid Id) : ICommand<Result>;
