namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterCursoPorIdQuery(Guid Id) : IQuery<CursoDto?>;
