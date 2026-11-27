namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Errors;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>Como o <see cref="GlobalExceptionMiddleware"/> trata uma exceção: quando se aplica, o que registrar e o que responder.</summary>
/// <param name="Casa">Verdadeiro quando a exceção é a que esta tradução trata.</param>
/// <param name="Registrar">Escreve o registro em log, sem dado do pedido.</param>
/// <param name="Escrever">Escreve a resposta de erro.</param>
internal sealed record TraducaoDeExcecao(
    Func<Exception, bool> Casa,
    Action<ILogger, PathString, Exception> Registrar,
    Func<HttpContext, Exception, IProblemTypeUriFactory, IDomainErrorMapper, Task> Escrever);
