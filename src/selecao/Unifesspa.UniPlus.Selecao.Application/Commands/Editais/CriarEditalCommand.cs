namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Comando para criação de um novo edital. Validado automaticamente pelo
/// <c>WolverineValidationMiddleware</c> via <c>CriarEditalCommandValidator</c>
/// antes de chegar ao handler — falhas de validação resultam em
/// <c>FluentValidation.ValidationException</c>, mapeada como ProblemDetails 400
/// pelo <c>GlobalExceptionMiddleware</c>.
/// </summary>
public sealed record CriarEditalCommand(
    int NumeroEdital,
    int AnoEdital,
    string Titulo,
    TipoProcesso TipoProcesso,
    int MaximoOpcoesCurso = 1) : ICommand<Result<Guid>>;
