namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;
using System.Reflection;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Binder de <see cref="PageRequest"/>: lê <c>cursor</c> e <c>limit</c> do
/// query string, valida limit contra <see cref="CursorPaginationOptions"/>,
/// decoda o cursor opaco (AES-GCM via <see cref="CursorEncoder"/>, ADR-0026)
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
    private const string CursorParam = "cursor";
    private const string LimitParam = "limit";

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        FromCursorAttribute attribute = ResolveAttribute(bindingContext)
            ?? throw new InvalidOperationException(
                $"Parâmetro do tipo {nameof(PageRequest)} requer [FromCursor(\"<recurso>\")].");

        IServiceProvider services = bindingContext.HttpContext.RequestServices;
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        CursorPaginationOptions options = services.GetRequiredService<IOptions<CursorPaginationOptions>>().Value;

        IQueryCollection query = bindingContext.HttpContext.Request.Query;

        // Lê e valida limit do query string ANTES de gastar AES no decode do cursor.
        int? requestedLimit = TryParseLimit(query[LimitParam]);
        if (requestedLimit is { } qsLimit && !IsInRange(qsLimit, options))
        {
            FailWith(bindingContext, CursorBindingErrorCodes.LimitInvalido,
                $"O parâmetro 'limit' deve estar entre {options.LimitMin} e {options.LimitMax}.");
            return;
        }

        Guid? afterId = null;
        int? cursorLimit = null;

        string? rawCursor = query[CursorParam];
        if (!string.IsNullOrWhiteSpace(rawCursor))
        {
            CursorDecodeResult decoded = await encoder
                .TryDecodeAsync(rawCursor, bindingContext.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            switch (decoded.Status)
            {
                case CursorDecodeStatus.Invalid:
                    FailWith(bindingContext, CursorBindingErrorCodes.Invalido, "O cursor informado é inválido.");
                    return;

                case CursorDecodeStatus.Expired:
                    FailWith(bindingContext, CursorBindingErrorCodes.Expirado, "O cursor informado expirou.");
                    return;

                case CursorDecodeStatus.Success:
                    if (!Guid.TryParse(decoded.Payload!.After, out Guid parsedAfter)
                        || !string.Equals(decoded.Payload.ResourceTag, attribute.Resource, StringComparison.Ordinal))
                    {
                        FailWith(bindingContext, CursorBindingErrorCodes.Invalido, "O cursor informado é inválido.");
                        return;
                    }

                    afterId = parsedAfter;
                    // Limit do cursor é "memória" do tamanho de janela; clampado se a
                    // config global apertou o range desde a emissão.
                    cursorLimit = Math.Clamp(decoded.Payload.Limit, options.LimitMin, options.LimitMax);
                    break;

                default:
                    FailWith(bindingContext, CursorBindingErrorCodes.Invalido, "O cursor informado é inválido.");
                    return;
            }
        }

        // Precedência: query string vence sobre cursor; cursor sobre default.
        // O keyset (afterId) é o que mantém estabilidade da janela, não o limit.
        int effectiveLimit = requestedLimit ?? cursorLimit ?? options.LimitDefault;

        bindingContext.Result = ModelBindingResult.Success(new PageRequest(afterId, effectiveLimit));
    }

    private static int? TryParseLimit(Microsoft.Extensions.Primitives.StringValues raw)
    {
        if (raw.Count == 0)
            return null;

        string? first = raw[0];
        if (string.IsNullOrWhiteSpace(first))
            return null;

        if (!int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return int.MinValue; // sentinel para "informado mas inválido" — cai no IsInRange como out-of-range

        return parsed;
    }

    private static bool IsInRange(int limit, CursorPaginationOptions options) =>
        limit >= options.LimitMin && limit <= options.LimitMax;

    private static void FailWith(ModelBindingContext bindingContext, string domainCode, string detail)
    {
        bindingContext.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey] = domainCode;
        bindingContext.ModelState.AddModelError(bindingContext.ModelName, detail);
        bindingContext.Result = ModelBindingResult.Failed();
    }

    private static FromCursorAttribute? ResolveAttribute(ModelBindingContext bindingContext)
    {
        // ParameterDescriptor.BindingInfo expõe o ModelBinderAttribute.BinderType, mas não
        // o FromCursorAttribute em si. O caminho confiável é olhar a ParameterInfo do
        // ControllerParameterDescriptor; o binder fica resiliente a renomes do parâmetro.
        if (bindingContext.ActionContext.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return null;

        foreach (Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor parameter in descriptor.Parameters)
        {
            if (parameter is not ControllerParameterDescriptor controllerParam)
                continue;

            if (controllerParam.ParameterType != typeof(PageRequest))
                continue;

            FromCursorAttribute? attribute = controllerParam.ParameterInfo
                .GetCustomAttribute<FromCursorAttribute>();
            if (attribute is not null)
                return attribute;
        }

        return null;
    }
}
