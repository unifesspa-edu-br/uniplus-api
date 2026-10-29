namespace Unifesspa.UniPlus.Selecao.API.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;

/// <summary>O que os filtros de acesso ao processo compartilham: o processo da rota e a resposta de quem não o alcança.</summary>
internal static class RotaDoProcesso
{
    /// <summary>Nome do parâmetro de rota dos recursos do próprio processo.</summary>
    public const string ParametroPadrao = "id";

    /// <summary>Nome do parâmetro de rota dos recursos aninhados do processo.</summary>
    public const string ParametroAninhado = "processoSeletivoId";

    // A rota exige o parâmetro como guid ({id:guid} ou {processoSeletivoId:guid}): um valor que não seja Guid nem chega ao filtro. Guid.Empty nunca existe, e cai na recusa.
    public static Guid Id(AuthorizationFilterContext context, string parametro = ParametroPadrao) =>
        context.RouteData.Values.TryGetValue(parametro, out object? id) && Guid.TryParse(id?.ToString(), out Guid processoId)
            ? processoId
            : Guid.Empty;

    /// <summary>A mesma resposta de processo inexistente: quem não pode alcançar o processo não descobre que ele existe.</summary>
    public static IActionResult NaoEncontrado(IServiceProvider servicos, Guid processoId) =>
        Result.Failure(new DomainError("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {processoId} não encontrado."))
            .ToActionResult(servicos.GetRequiredService<IDomainErrorMapper>());

    /// <summary>O <c>403</c> de quem tem a permissão mas está inscrito no processo.</summary>
    public static IActionResult Impedido(IServiceProvider servicos) =>
        Result.Failure(AcessoAoProcesso.ImpedidoPorInscricao)
            .ToActionResult(servicos.GetRequiredService<IDomainErrorMapper>());
}
