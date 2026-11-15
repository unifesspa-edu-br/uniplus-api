namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;

using Idempotency;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;

/// <summary>
/// Resolve a declaração <see cref="EmiteEsperaEsgotadaPelaTravaAttribute"/> de uma ação, a mesma para o contrato, a política
/// de CORS e a resposta em execução, para que nenhum dos três diga o que os outros não dizem.
/// </summary>
internal static class DeclaracaoDaEsperaEsgotada
{
    /// <summary>A declaração da ação, ou da classe quando a ação não tem; nula quando a rota não declara.</summary>
    internal static EmiteEsperaEsgotadaPelaTravaAttribute? Da(ActionDescriptor? acao) =>
        acao is ControllerActionDescriptor controller
            ? controller.MethodInfo.GetCustomAttribute<EmiteEsperaEsgotadaPelaTravaAttribute>(inherit: true)
                ?? controller.ControllerTypeInfo.GetCustomAttribute<EmiteEsperaEsgotadaPelaTravaAttribute>(inherit: true)
            : null;

    /// <summary>Se a rota em curso exige <c>Idempotency-Key</c>; só então a orientação de reenvio fala em nova chave.</summary>
    internal static bool ExigeChaveDeIdempotencia(HttpContext http) =>
        ExigeChaveDeIdempotencia(http.GetEndpoint()?.Metadata.GetMetadata<ActionDescriptor>());

    /// <summary>Se a ação, ou a classe dela, exige <c>Idempotency-Key</c>.</summary>
    internal static bool ExigeChaveDeIdempotencia(ActionDescriptor? acao) =>
        acao is ControllerActionDescriptor controller
        && (controller.MethodInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)
            ?? controller.ControllerTypeInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)) is not null;
}
