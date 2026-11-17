namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using System.Net.Http.Json;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Monta a requisição de escrita das rotas administrativas da Organização Institucional: autenticada como
/// administrador da plataforma, com a Idempotency-Key obrigatória e o corpo em JSON.
/// </summary>
internal static class RequisicaoAdministrativa
{
    public static HttpRequestMessage Criar(HttpMethod metodo, string recurso, object corpo)
    {
        HttpRequestMessage requisicao = new(metodo, new Uri($"/api/organizacao/admin/{recurso}", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        requisicao.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        requisicao.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return requisicao;
    }
}
