namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

/// <summary>
/// Aplica o <see cref="IdempotencyFilter"/> a cada controller do processo, uma vez só: o filtro age apenas nas actions
/// marcadas com <see cref="RequiresIdempotencyKeyAttribute"/>, e a convention não readiciona o filtro a um controller que
/// já o tem.
/// </summary>
internal sealed class IdempotencyControllerConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (ControllerModel controller in application.Controllers.Where(static c => !JaTemOFiltro(c)))
        {
            // IsReusable fica false: o filtro depende do contexto scoped, então é instanciado por request.
            controller.Filters.Add(new TypeFilterAttribute(typeof(IdempotencyFilter)));
        }
    }

    private static bool JaTemOFiltro(ControllerModel controller) =>
        controller.Filters.Any(static f => f is TypeFilterAttribute tf && tf.ImplementationType == typeof(IdempotencyFilter));
}
