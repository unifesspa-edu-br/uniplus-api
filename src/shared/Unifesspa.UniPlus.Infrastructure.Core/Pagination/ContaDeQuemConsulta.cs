namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Resumo da conta de quem consulta (par emissor e identificador da conta). É o que o cursor leva no lugar da
/// identificação, que não tem garantia de ser opaca. Escritor e leitor da página usam esta mesma função.
/// </summary>
internal static class ContaDeQuemConsulta
{
    /// <summary>Nulo quando não há conta autenticada com emissor e identificador.</summary>
    public static string? Resumir(IUserContext user) =>
        user.IsAuthenticated && !string.IsNullOrEmpty(user.Issuer) && !string.IsNullOrEmpty(user.UserId)
            ? ResumoCanonico.De(user.Issuer, user.UserId)
            : null;
}
