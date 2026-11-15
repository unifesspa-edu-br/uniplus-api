namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Apoio comum aos testes de integração do admin de <c>ObrigatoriedadeLegal</c>: cliente com a
/// concessão de manutenção, envio com chave de idempotência e limpeza das regras criadas.
/// </summary>
public abstract class ObrigatoriedadeLegalAdminTestBase : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";

    // Predicados em Dictionary porque anonymous types não suportam keys que
    // começam com '$' (o JsonPolymorphic do PredicadoObrigatoriedade usa
    // "$tipo" como discriminator per ADR-0058).
    protected static readonly Dictionary<string, object> PredicadoConcorrenciaDupla = new(StringComparer.Ordinal)
    {
        ["$tipo"] = "concorrenciaDuplaObrigatoria",
    };

    protected ObrigatoriedadeLegalAdminTestBase(CascadingFixture fixture)
    {
        Fixture = fixture;
    }

    protected CascadingFixture Fixture { get; }

    public async Task InitializeAsync() =>
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Leandro);

    /// <summary>
    /// A <see cref="CascadingFixture"/> compartilha UM Postgres entre TODAS as classes da
    /// coleção (Story #853 tornou isso perigoso): uma <c>ObrigatoriedadeLegal</c> universal
    /// (<c>"*"</c>) que esta classe cria com vigência aberta sobreviveria para o resto da
    /// suíte, e o gate legal novo passaria a avaliar TODO <c>Publicar</c>/<c>Retificar</c>/
    /// <c>FecharRetificacao</c> de OUTRAS classes contra ela — reprovando processos que nunca
    /// tiveram nada a ver com esta suíte. Soft-delete de tudo que esta classe criou
    /// (identificado pelo prefixo <c>TEST_</c> de <see cref="UniqueRegraCodigo"/>) devolve o
    /// catálogo ao estado anterior para a próxima classe da coleção.
    /// </summary>
    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await using UniPlusDbContext db = ResolveDbContext(scope);

        await db.Database.ExecuteSqlAsync(
            $"UPDATE selecao.obrigatoriedades_legais SET is_deleted = true WHERE regra_codigo LIKE 'TEST\\_%' ESCAPE '\\'");
    }

    // O token só identifica Leandro; quem lhe dá a permissão é a concessão gravada do perfil.
    protected async Task<HttpClient> ClienteComConcessaoAsync()
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            Fixture.Factory.Services, Leandro, PermissaoSelecao.ObrigatoriedadesLegaisManter.Codigo);
        HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Leandro);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Leandro Viana");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "leandro.viana@unifesspa.edu.br");
        return client;
    }

    protected static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object payload, string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(payload);

        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(url, UriKind.Relative));
        request.Content = JsonContent.Create(payload);
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    protected static async Task<HttpResponseMessage> PutAsync(HttpClient client, string url, object payload)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(payload);

        using HttpRequestMessage request = new(HttpMethod.Put, new Uri(url, UriKind.Relative));
        request.Content = JsonContent.Create(payload);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    protected static UniPlusDbContext ResolveDbContext(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

    protected static string UniqueRegraCodigo() =>
        $"TEST_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}_{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
