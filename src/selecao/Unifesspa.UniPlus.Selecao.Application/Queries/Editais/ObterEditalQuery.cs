namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using MediatR;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterEditalQuery(Guid Id) : IRequest<Result<EditalDto>>;
