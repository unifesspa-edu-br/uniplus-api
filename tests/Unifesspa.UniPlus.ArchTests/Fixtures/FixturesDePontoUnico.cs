namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using System.Security.Claims;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Tipos mínimos que servem de contraprova à regra do ponto único de decisão: os três
/// primeiros decidem acesso por fora dele e têm de ser rejeitados; o último decide pela porta
/// do ponto único e tem de ser aceito.
/// </summary>
internal static class FixturesDePontoUnico
{
    internal sealed class DecidePeloPapelDoPrincipal(ClaimsPrincipal principal)
    {
        public bool Pode() => principal.IsInRole("coordenador");
    }

    internal sealed class DecidePelaClaimDoPrincipal(ClaimsPrincipal principal)
    {
        public bool Pode() => principal.HasClaim("cargo", "diretor");
    }

    internal sealed class DecidePeloPapelDoContexto(IUserContext usuario)
    {
        public bool Pode() => usuario.HasRole("coordenador");
    }

    internal sealed class ChamaOServicoDeDecisaoSemPassarPelaPorta(IAuthorizationDecisionService decisao)
    {
        public IAuthorizationDecisionService Decisao => decisao;
    }

    internal sealed class DecidePelaPortaDoPontoUnico(IVerificadorDeAcesso verificador)
    {
        public Task<ResultadoDoAcesso> PodeAsync(PermissionRequirement permissao, CancellationToken cancellationToken)
            => verificador.VerificarAsync(permissao, cancellationToken: cancellationToken);
    }
}
