namespace Unifesspa.UniPlus.Configuracao.Application.Queries.RecursosAcessibilidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterRecursoAcessibilidadePorIdQuery(Guid Id) : IQuery<RecursoAcessibilidadeDto?>;
