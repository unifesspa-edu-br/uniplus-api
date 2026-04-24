namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

/// <summary>
/// Claims institucionais expostos pelo scope <c>uniplus-profile</c> do realm
/// <c>unifesspa</c> (Keycloak). Estes nomes são contrato do produto Uni+:
/// são <b>nossos</b>, não vêm do OIDC Core nem são proprietários do Keycloak.
/// </summary>
/// <remarks>
/// <para>
/// Se o realm for reconfigurado ou o IdP for trocado, os mesmos <c>claim.name</c>
/// precisam ser recriados no novo provider para preservar o contrato.
/// </para>
/// <para>
/// Configuração atual: <c>docker/keycloak/realm-export.json</c> — scope
/// <c>uniplus-profile</c>, protocol mappers <c>cpf</c> e <c>nomeSocial</c>.
/// </para>
/// </remarks>
internal static class UniPlusClaims
{
    public const string Cpf = "cpf";
    public const string NomeSocial = "nomeSocial";
}
