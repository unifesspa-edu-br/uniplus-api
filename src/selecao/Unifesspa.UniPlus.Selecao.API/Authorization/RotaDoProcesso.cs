namespace Unifesspa.UniPlus.Selecao.API.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O que os filtros de acesso ao processo compartilham: o processo da rota e a resposta de quem não o alcança.</summary>
internal static class RotaDoProcesso
{
    // A rota exige {id:guid}: um valor que não seja Guid nem chega ao filtro. Guid.Empty nunca existe, e cai na recusa.
    public static Guid Id(AuthorizationFilterContext context) =>
        context.RouteData.Values.TryGetValue("id", out object? id) && Guid.TryParse(id?.ToString(), out Guid processoId)
            ? processoId
            : Guid.Empty;

    /// <summary>A mesma resposta de processo inexistente: quem não pode alcançar o processo não descobre que ele existe.</summary>
    public static IActionResult NaoEncontrado(IServiceProvider servicos, Guid processoId) =>
        Result.Failure(new DomainError("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {processoId} não encontrado."))
            .ToActionResult(servicos.GetRequiredService<IDomainErrorMapper>());
}
