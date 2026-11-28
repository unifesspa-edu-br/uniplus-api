namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Operações HTTP e montagem de payloads compartilhadas pelas classes de teste
/// dos endpoints de <c>AtoNormativo</c>.
/// </summary>
internal sealed class AtoNormativoApoio(PublicacoesEndpointFixture fixture)
{
    internal const string BaseAtos = "/api/publicacoes/atos";
    internal const string AdminAtos = "/api/publicacoes/admin/atos";
    internal const string AdminTipos = "/api/publicacoes/admin/tipos-ato";
    internal const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    internal const string OutroHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    internal const string Publicacao = "2026-03-13";

    internal PublicacoesEndpointFixture Fixture { get; } = fixture;

    internal static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    internal static object PayloadAto(string tipoCodigo, string serie, string? numero) =>
        new
        {
            orgao = "CEPS",
            serie,
            ano = 2026,
            numero,
            tipoCodigo,
            dataDoAto = Publicacao,
            documentoHash = HashValido,
            assinante = PersonasDeTeste.NomeKevinCarvalho,
        };

    internal static object PayloadComVersaoDoObjeto(string tipoCodigo, Guid versaoId, string? versaoHash) =>
        new
        {
            orgao = "CEPS",
            serie = SerieUnica(),
            ano = 2026,
            numero = "1",
            tipoCodigo,
            dataDoAto = Publicacao,
            documentoHash = HashValido,
            assinante = PersonasDeTeste.NomeKevinCarvalho,
            versaoDoObjetoId = versaoId,
            versaoDoObjetoHash = versaoHash,
        };

    internal static object PayloadComAnexos(
        string tipoCodigo, object anexos, string? serie = null, object? vinculos = null) =>
        new
        {
            orgao = "CEPS",
            serie = serie ?? SerieUnica(),
            ano = 2026,
            numero = "1",
            tipoCodigo,
            dataDoAto = Publicacao,
            documentoHash = HashValido,
            assinante = PersonasDeTeste.NomeKevinCarvalho,
            anexos,
            vinculos,
        };

    internal static object PayloadRetificacao(
        string tipoCodigo, string serie, string? numero, Guid atoRetificadoId, string motivo) =>
        new
        {
            orgao = "CEPS",
            serie,
            ano = 2026,
            numero,
            tipoCodigo,
            dataDoAto = Publicacao,
            documentoHash = HashValido,
            assinante = PersonasDeTeste.NomeKevinCarvalho,
            atoRetificadoId,
            motivoRetificacao = motivo,
        };

    internal static async Task<HttpResponseMessage> PostAtoAsync(
        HttpClient client, object payload, string? chave = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(payload);

        return await client.SendAsync(request);
    }

    internal static async Task<JsonDocument> PostAtoCriadoAsync(HttpClient client, object payload)
    {
        HttpResponseMessage resposta = await PostAtoAsync(client, payload);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    internal static async Task<Guid> PostAtoIdAsync(HttpClient client, object payload)
    {
        using JsonDocument criado = await PostAtoCriadoAsync(client, payload);
        return criado.RootElement.GetProperty("atoId").GetGuid();
    }

    internal static async Task<JsonElement> LerAtoAsync(HttpClient client, Guid atoId)
    {
        using JsonDocument doc = JsonDocument.Parse(
            await client.GetStringAsync(new Uri($"{BaseAtos}/{atoId}", UriKind.Relative)));
        return doc.RootElement.Clone();
    }

    internal static async Task<JsonElement> ProcurarNaListagemAsync(HttpClient client, Guid atoId)
    {
        string? url = $"{BaseAtos}?limit=100";
        while (url is not null)
        {
            (JsonElement? achado, url) = await LerPaginaDaListagemAsync(client, url, atoId);
            if (achado is not null)
            {
                return achado.Value;
            }
        }

        throw new InvalidOperationException($"O ato {atoId} não apareceu na listagem.");
    }

    private static async Task<(JsonElement? Achado, string? ProximaPagina)> LerPaginaDaListagemAsync(
        HttpClient client, string url, Guid atoId)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (ProcurarNaPagina(await resposta.Content.ReadAsStringAsync(), atoId), LinkDe(resposta, "next"));
    }

    internal static string SerieUnica() => "EDITAL_" + Sufixo();

    internal async Task<string> CriarTipoVigenteAsync(bool congela = false, bool efeito = false) =>
        (await CadastrarTipoVigenteAsync(congela: congela, efeito: efeito)).Codigo;

    internal async Task<(string Codigo, Guid Id)> CadastrarTipoVigenteAsync(
        string nome = "Tipo para ato de teste", bool congela = false, bool efeito = false)
    {
        string codigo = "ATO_TIPO_" + Sufixo();
        using HttpClient client = Fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminTipos, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(PayloadTipoVigente(codigo, nome, congela, efeito));

        HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (codigo, Guid.Parse(response.Headers.Location!.Segments[^1], CultureInfo.InvariantCulture));
    }

    internal async Task<bool> ExisteAtoNaSerieAsync(string serie)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext ctx = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await ctx.AtosNormativos.AnyAsync(a => a.Serie == serie);
    }

    internal static string Sufixo()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }

    private static object PayloadTipoVigente(string codigo, string nome, bool congela, bool efeito) =>
        new
        {
            codigo,
            nome,
            congelaConfiguracao = congela,
            unicoPorObjeto = false,
            efeitoIrreversivel = efeito,
            ehResultado = false,
            publicavelComoAvulso = false,
            exigeAtoCorrigido = false,
            vigenciaInicio = "2020-01-01",
            vigenciaFim = (string?)null,
        };

    private static JsonElement? ProcurarNaPagina(string corpo, Guid atoId)
    {
        using JsonDocument pagina = JsonDocument.Parse(corpo);
        foreach (JsonElement ato in pagina.RootElement.EnumerateArray())
        {
            if (ato.GetProperty("id").GetGuid() == atoId)
            {
                return ato.Clone();
            }
        }

        return null;
    }

    /// <summary>O destino do cabeçalho <c>Link</c> com a relação pedida (<c>next</c>, <c>prev</c>), ou nulo quando a resposta não a traz.</summary>
    internal static string? LinkDe(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? valores))
        {
            return null;
        }

        string? parte = string.Join(',', valores).Split(',')
            .FirstOrDefault(p => p.Contains($"rel=\"{rel}\"", StringComparison.Ordinal));

        return parte?[(parte.IndexOf('<', StringComparison.Ordinal) + 1)..parte.IndexOf('>', StringComparison.Ordinal)];
    }
}
