namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lê uma revisão com os itens.</summary>
public sealed record ObterRevisaoQuery(Guid Id) : IQuery<Result<RevisaoDeAcessosDto>>;
