namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Binder de <see cref="PageRequest"/>: lê <c>cursor</c> e <c>limit</c> do
/// query string, valida limit contra <see cref="CursorPaginationOptions"/>,
/// decodifica o cursor opaco (AES-GCM via <see cref="CursorEncoder"/>, ADR-0026)
/// e produz um <see cref="PageRequest"/> tipado para a action.
/// <para>
/// Erros de parsing publicam o código de domínio em
/// <c>HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]</c> e
/// adicionam mensagem ao <c>ModelState</c>; o
/// <see cref="CursorPaginationProblemFactory"/> traduz para o HTTP status
/// correto (400/410/422) via <c>IDomainErrorMapper</c>.
/// </para>
/// </summary>
public sealed class PageRequestModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        PageRequest? page = await CreateReader(bindingContext)
            .ReadOrRejectAsync()
            .ConfigureAwait(false);
        if (page is not null)
        {
            bindingContext.Result = ModelBindingResult.Success(page);
        }
    }

    /// <summary>
    /// Resolve, nesta ordem, o atributo (que lança se faltar), o <see cref="CursorEncoder"/> e as opções
    /// de paginação; o leitor não resolve nada ao ser construído.
    /// </summary>
    private static PageRequestReader CreateReader(ModelBindingContext bindingContext)
    {
        FromCursorAttribute attribute = FromCursorAttribute.Of(bindingContext.ActionContext.ActionDescriptor)
            ?? throw new InvalidOperationException(
                $"Parâmetro do tipo {nameof(PageRequest)} requer [FromCursor(\"<recurso>\")].");

        IServiceProvider services = bindingContext.HttpContext.RequestServices;
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        CursorPaginationOptions options = services.GetRequiredService<IOptions<CursorPaginationOptions>>().Value;

        return new PageRequestReader(bindingContext, attribute, encoder, options);
    }
}
