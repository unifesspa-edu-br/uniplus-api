namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using MediatR;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record CriarEditalCommand(
    int NumeroEdital,
    int AnoEdital,
    string Titulo,
    TipoProcesso TipoProcesso,
    int MaximoOpcoesCurso = 1) : IRequest<Result<Guid>>;
