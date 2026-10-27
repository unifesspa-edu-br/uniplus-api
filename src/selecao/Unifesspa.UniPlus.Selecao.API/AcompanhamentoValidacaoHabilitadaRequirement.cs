namespace Unifesspa.UniPlus.Selecao.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

/// <summary>
/// Libera as consultas de acompanhamento só com a validação temporária habilitada. Decidido na
/// autorização, antes do model binding e da negociação de mídia, para que a opção desligada
/// responda 403 independentemente dos parâmetros.
/// </summary>
internal sealed class AcompanhamentoValidacaoHabilitadaRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "AcompanhamentoValidacaoHabilitada";
}

internal sealed class AcompanhamentoValidacaoHabilitadaHandler(IOptions<AcompanhamentoOptions> options)
    : AuthorizationHandler<AcompanhamentoValidacaoHabilitadaRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AcompanhamentoValidacaoHabilitadaRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (options.Value.ValidacaoHabilitada)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
