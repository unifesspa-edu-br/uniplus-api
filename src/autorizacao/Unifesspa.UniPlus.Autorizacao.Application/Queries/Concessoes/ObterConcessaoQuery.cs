namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Obtém uma concessão pelo Id. Falha com <c>nao_encontrada</c> quando inexistente.</summary>
public sealed record ObterConcessaoQuery(Guid Id) : IQuery<Result<ConcessaoDto>>;

/// <summary>Obtém a trilha de uma concessão, em ordem cronológica.</summary>
public sealed record ObterTrilhaDaConcessaoQuery(Guid Id) : IQuery<Result<IReadOnlyList<EventoDaTrilhaDto>>>;
