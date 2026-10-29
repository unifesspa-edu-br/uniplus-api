namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A identidade (emissor e sub) de quem executa a operação, tirada do token.</summary>
internal static class IdentidadeDoToken
{
    /// <summary>
    /// A identidade do token, com o nome de exibição, ou nulo quando faltam emissor ou sub: sem autor identificado nada é
    /// gravado.
    /// </summary>
    public static Identidade? De(IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        return string.IsNullOrWhiteSpace(userContext.Issuer) || string.IsNullOrWhiteSpace(userContext.UserId)
            ? null
            : new Identidade(userContext.Issuer, userContext.UserId, NomeDe(userContext));
    }

    /// <summary>
    /// A pessoa do token, com o nome e o e-mail que ele traz, ou nulo quando faltam emissor ou sub. O nome é o social
    /// quando há, senão o civil (<see cref="NomeDe"/>).
    /// </summary>
    public static PessoaNoToken? PessoaDe(IUserContext userContext) =>
        De(userContext) is { } identidade
            ? new PessoaNoToken(identidade, identidade.Nome, userContext.Email, userContext.EmailVerificado)
            : null;

    /// <summary>
    /// O nome social quando a pessoa tem, senão o civil do token; nulo sem nenhum dos dois, porque o nome de exibição do
    /// provedor pode ser o identificador de login.
    /// </summary>
    internal static string? NomeDe(IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        return NomeDeExibicao.Normalizar(!string.IsNullOrWhiteSpace(userContext.NomeSocial) ? userContext.NomeSocial : userContext.NomeCompleto);
    }
}
