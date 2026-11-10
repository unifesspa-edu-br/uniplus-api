namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Errors;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pagination;

using Unifesspa.UniPlus.Authorization.Abstractions;

public sealed partial class GlobalExceptionMiddleware
{
    // A primeira entrada cujo tipo casa com a exceção traduz; a exceção desconhecida cai em NaoTratada.
    private static readonly TraducaoDeExcecao[] Traducoes =
    [
        new(
            typeof(ValidationException),
            static (l, p, e) => LogValidationError(l, p, e),
            static (c, e, _, m) => EscreverRespostaValidacao(c, (ValidationException)e, m)),
        new(
            typeof(CursorAnchorMismatchException),
            static (l, p, e) => LogAncoraDeCursorInvalida(l, p, e),
            static (c, _, f, _) => EscreverRespostaCursorInvalido(c, f)),
        new(
            typeof(DbUpdateConcurrencyException),
            static (l, p, e) => LogConflitoDeConcorrencia(l, p, e),
            static (c, _, f, _) => EscreverRespostaConflitoDeConcorrencia(c, f)),
        new(
            typeof(RegistroDeAcessoNaoGravadoException),
            static (l, p, e) => LogRegistroDeAcessoNaoGravado(l, p, e),
            static (c, _, f, _) => EscreverRespostaRegistroDeAcessoNaoGravado(c, f)),
        new(
            typeof(CatalogoDesatualizadoException),
            static (l, p, e) => LogCatalogoDesatualizado(l, p, e),
            static (c, _, f, _) => EscreverRespostaCatalogoDesatualizado(c, f)),
    ];

    private static readonly TraducaoDeExcecao NaoTratada = new(
        typeof(Exception),
        static (l, p, e) => LogUnhandledError(l, p, e),
        static (c, _, f, _) => EscreverRespostaErro(c, f));

    private sealed record TraducaoDeExcecao(
        Type Tipo,
        Action<ILogger, PathString, Exception> Registrar,
        Func<HttpContext, Exception, IProblemTypeUriFactory, IDomainErrorMapper, Task> Escrever);
}
