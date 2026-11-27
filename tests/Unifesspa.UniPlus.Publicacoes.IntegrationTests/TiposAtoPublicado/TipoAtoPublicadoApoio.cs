namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Operações HTTP e montagem de payloads compartilhadas pelas classes de teste
/// dos endpoints de <c>TipoAtoPublicado</c>.
/// </summary>
internal sealed class TipoAtoPublicadoApoio(PublicacoesEndpointFixture fixture)
{
    internal const string Base = "/api/publicacoes/tipos-ato";
    internal const string Admin = "/api/publicacoes/admin/tipos-ato";
    internal const string Inicio = "2026-01-01";

    internal static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    internal static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, CorpoDoTipoDeAto corpo, string? chave = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(corpo);

        return await client.SendAsync(request);
    }

    /// <summary>Lê a representação de um recurso: o GET tem de responder 200, e quem chama descarta o documento.</summary>
    internal static async Task<JsonDocument> LerAsync(HttpClient client, Uri destino)
    {
        HttpResponseMessage resposta = await client.GetAsync(destino);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    internal static async Task<HttpResponseMessage> AtualizarAsync(
        HttpClient client, Guid idDaRota, CorpoDoTipoDeAto corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"{Admin}/{idDaRota}", UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(corpo);

        return await client.SendAsync(request);
    }

    /// <summary>Cadastra o tipo pelo POST e devolve o id; o cadastro tem de responder 201.</summary>
    internal static async Task<Guid> CadastrarAsync(HttpClient client, CorpoDoTipoDeAto corpo)
    {
        HttpResponseMessage response = await PostAsync(client, corpo);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return JsonSerializer.Deserialize<Guid>(await response.Content.ReadAsStringAsync());
    }

    internal async Task<Guid> CriarAsync(
        string codigo, string inicio = Inicio, string? fim = null, bool congela = true)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        return await CadastrarAsync(
            client,
            new CorpoDoTipoDeAto(codigo) { VigenciaInicio = inicio, VigenciaFim = fim, CongelaConfiguracao = congela });
    }

    /// <summary>Apaga de vez os tipos de ato com os códigos informados, que o teste cadastrou.</summary>
    internal async Task RemoverPorCodigosAsync(IReadOnlyCollection<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        string[] apagar = [.. codigos];
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM publicacoes.tipos_de_ato_publicado WHERE codigo = ANY({apagar})");
    }

    /// <summary>Código único no formato UPPER_SNAKE, para não colidir entre testes.</summary>
    internal static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "ENDPOINT_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }
}
