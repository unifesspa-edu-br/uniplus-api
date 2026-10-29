namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A identidade (emissor e sub) de quem executa a operação, tirada do token.</summary>
internal static class IdentidadeDoToken
{
    /// <summary>A identidade do token, ou nulo quando faltam emissor ou sub: sem autor identificado nada é gravado.</summary>
    public static Identidade? De(IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        return string.IsNullOrWhiteSpace(userContext.Issuer) || string.IsNullOrWhiteSpace(userContext.UserId)
            ? null
            : new Identidade(userContext.Issuer, userContext.UserId);
    }
}
