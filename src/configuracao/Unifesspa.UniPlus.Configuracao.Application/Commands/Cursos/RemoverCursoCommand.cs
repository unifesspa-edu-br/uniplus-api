namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) um curso pelo seu <c>Id</c>.</summary>
public sealed record RemoverCursoCommand(Guid Id) : ICommand<Result>;
