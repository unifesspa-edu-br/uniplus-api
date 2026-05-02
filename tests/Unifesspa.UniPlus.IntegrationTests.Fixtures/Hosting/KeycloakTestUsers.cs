namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Catálogo dos usuários sintéticos provisionados pelo realm de testes E2E
/// (<c>docker/keycloak/realm-e2e-tests.json</c>). Centralizar aqui evita
/// duplicar literais (CPF, email, nomeSocial, role) entre o realm e cada teste,
/// reduzindo a fragilidade quando o realm sintético precisar evoluir — qualquer
/// alteração no JSON é refletida num único ponto consumido por todas as suítes.
/// </summary>
public static class KeycloakTestUsers
{
    /// <summary>Senha compartilhada por todos os usuários sintéticos do realm de teste.</summary>
    public const string SharedPassword = "Changeme!123";

    public static readonly KeycloakTestUser Admin = new(
        Username: "admin",
        Email: "admin@e2e.uniplus.local",
        Cpf: "52998224725",
        NomeSocial: "Admin Teste",
        Role: "admin");

    public static readonly KeycloakTestUser Gestor = new(
        Username: "gestor",
        Email: "gestor@e2e.uniplus.local",
        Cpf: "11144477735",
        NomeSocial: "Gestor Teste",
        Role: "gestor");

    public static readonly KeycloakTestUser Avaliador = new(
        Username: "avaliador",
        Email: "avaliador@e2e.uniplus.local",
        Cpf: "39053344705",
        NomeSocial: "Avaliador Teste",
        Role: "avaliador");

    public static readonly KeycloakTestUser Candidato = new(
        Username: "candidato",
        Email: "candidato@e2e.uniplus.local",
        Cpf: "24843803480",
        NomeSocial: "Candidato Teste",
        Role: "candidato");
}

/// <summary>
/// Snapshot dos atributos de um usuário sintético do realm de testes — espelha o
/// estado declarado em <c>realm-e2e-tests.json</c>. CPFs são valores publicamente
/// reconhecidos como válidos quanto ao algoritmo, sem correspondência a pessoas reais.
/// </summary>
public sealed record KeycloakTestUser(
    string Username,
    string Email,
    string Cpf,
    string NomeSocial,
    string Role);
