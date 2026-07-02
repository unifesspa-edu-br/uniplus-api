namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) uma oferta de curso pelo seu <c>Id</c>.</summary>
public sealed record RemoverOfertaCursoCommand(Guid Id) : ICommand<Result>;
