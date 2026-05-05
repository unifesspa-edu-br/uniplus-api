namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Errors;

/// <summary>
/// Mapeamento dos códigos de cursor pagination produzidos pelo
/// <see cref="PageRequestModelBinder"/>. Vive em <c>Infrastructure.Core</c>
/// porque a capability é cross-module: qualquer módulo (Selecao, Ingresso,
/// futuros) que use <c>[FromCursor]</c> herda os mesmos codes públicos
/// (<c>uniplus.cursor.*</c>) sem precisar duplicar registration.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider em AddCursorPagination().")]
internal sealed class PaginationDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(CursorBindingErrorCodes.Invalido,
            new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.cursor.invalido", "Cursor de paginação inválido")),
        new(CursorBindingErrorCodes.Expirado,
            new DomainErrorMapping(StatusCodes.Status410Gone, "uniplus.cursor.expirado", "Cursor de paginação expirado")),
        new(CursorBindingErrorCodes.LimitInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.cursor.limit_invalido", "Tamanho de página inválido")),
    ];
}
