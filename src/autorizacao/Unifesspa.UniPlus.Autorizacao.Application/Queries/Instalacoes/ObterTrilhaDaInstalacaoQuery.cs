namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Trilha da instalação, inclusive a cancelada ou expirada, que não gerou concessão.</summary>
public sealed record ObterTrilhaDaInstalacaoQuery(Guid Id) : IQuery<Result<IReadOnlyList<EventoDaTrilhaDto>>>;
