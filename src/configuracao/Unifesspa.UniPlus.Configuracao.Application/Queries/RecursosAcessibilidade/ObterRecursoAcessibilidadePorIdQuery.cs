namespace Unifesspa.UniPlus.Configuracao.Application.Queries.RecursosAcessibilidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterRecursoAcessibilidadePorIdQuery(Guid Id) : IQuery<Result<RecursoAcessibilidadeDto>>;
