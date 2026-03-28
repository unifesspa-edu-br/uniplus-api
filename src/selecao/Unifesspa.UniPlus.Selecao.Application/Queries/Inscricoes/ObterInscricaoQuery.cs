namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using MediatR;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed record ObterInscricaoQuery(Guid Id) : IRequest<Result<InscricaoDto>>;
