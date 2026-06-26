namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Aplica o <see cref="IdempotencyFilter{TDbContext}"/> apenas aos controllers
/// do assembly do módulo (não como filtro global). No monólito modular — vários
/// módulos co-hospedados no mesmo processo — isso garante que cada controller
/// receba exatamente UM filtro de idempotência: o do seu próprio módulo, fechado
/// em <typeparamref name="TDbContext"/> e, portanto, ligado à tabela
/// <c>idempotency_cache</c> no schema correto.
/// </summary>
/// <remarks>
/// Substitui o antigo <c>PostConfigure&lt;MvcOptions&gt;</c> que adicionava um
/// filtro global por chamada de <c>AddIdempotency</c>: como esse hook é
/// acumulativo, N módulos co-hospedados instalavam N filtros globais — o 1º
/// reservava a chave (Processing) e o 2º respondia 409, quebrando todo endpoint
/// idempotente. O guard abaixo também torna a aplicação idempotente caso a
/// convention seja registrada mais de uma vez para o mesmo módulo.
/// </remarks>
internal sealed class IdempotencyControllerConvention<TDbContext> : IApplicationModelConvention
    where TDbContext : DbContext
{
    private readonly Assembly _moduleAssembly;

    public IdempotencyControllerConvention(Assembly moduleAssembly)
    {
        ArgumentNullException.ThrowIfNull(moduleAssembly);
        _moduleAssembly = moduleAssembly;
    }

    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (ControllerModel controller in application.Controllers)
        {
            if (controller.ControllerType.Assembly != _moduleAssembly)
            {
                continue;
            }

            bool jaAplicado = controller.Filters.Any(f =>
                f is TypeFilterAttribute tf
                && tf.ImplementationType == typeof(IdempotencyFilter<TDbContext>));

            if (jaAplicado)
            {
                continue;
            }

            // IsReusable fica false (default): o filtro depende de
            // EfCoreIdempotencyStore<TDbContext>/DbContext scoped, então precisa
            // ser instanciado por request.
            controller.Filters.Add(new TypeFilterAttribute(typeof(IdempotencyFilter<TDbContext>)));
        }
    }
}
