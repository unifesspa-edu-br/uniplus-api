namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Kernel.Results;

/// <summary>
/// Comando para criação de um novo edital. Validado automaticamente pelo
/// <c>WolverineValidationMiddleware</c> via <c>CriarEditalCommandValidator</c>
/// antes de chegar ao handler — falhas de validação resultam em
/// <c>FluentValidation.ValidationException</c>, mapeada como ProblemDetails 400
/// pelo <c>GlobalExceptionMiddleware</c>.
/// </summary>
/// <remarks>
/// <para><c>TipoEditalId</c> é a FK preparatória para a futura entidade
/// <c>TipoEdital</c> (Story #455 — promove o enum <c>TipoProcesso</c> em
/// entidade). Permanece opcional/nulo nesta Story #454 porque a entidade
/// ainda não existe em <c>Selecao.Domain</c>; quando #455 introduzir o
/// agregado e o seed Newman (#463) popular as linhas-template, o campo
/// passa a ser obrigatório por validador.</para>
/// </remarks>
public sealed record CriarEditalCommand(
    int NumeroEdital,
    int AnoEdital,
    string Titulo,
    Guid? TipoEditalId = null,
    int MaximoOpcoesCurso = 1) : ICommand<Result<Guid>>;
