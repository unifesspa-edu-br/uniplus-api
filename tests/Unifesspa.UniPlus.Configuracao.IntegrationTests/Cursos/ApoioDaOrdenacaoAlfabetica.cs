namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Pagination;

internal sealed class ApoioDaOrdenacaoAlfabetica
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    internal ApoioDaOrdenacaoAlfabetica(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    internal const string Cursos = "/api/configuracao/cursos";

    internal const string OfertasCurso = "/api/configuracao/ofertas-curso";

    // Cursor legítimo — cifrado pela chave do próprio host, com a etiqueta e a direção certas — mas cuja chave de
    // ordenação tem um número de colunas diferente do que esta listagem ordena. É o que um cliente veria ao
    // reapresentar, sob uma ordenação, o cursor emitido por outra.
    internal async Task<string> CursorDeOutraOrdenacaoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        CursorEncoder encoder = escopo.ServiceProvider.GetRequiredService<CursorEncoder>();

        return await encoder.EncodeAsync(new CursorPayload(
            After: Guid.CreateVersion7().ToString(),
            Limit: 20,
            ResourceTag: "cursos",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            Direction: PaginationDirection.Next,
            SortKey: CompositeSortKey.Serialize("uma", "chave", "de tres colunas")));
    }

    internal static string Bloco() => $"ZZZ{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    internal static async Task<HttpResponseMessage> SeguirAtePaginaComAsync(HttpClient client, string bloco, string url)
    {
        string? atual = url;
        while (atual is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(atual, UriKind.RelativeOrAbsolute));
            IReadOnlyList<string> nomes = await LerNomesAsync(resposta);
            if (nomes.Any(n => n.StartsWith(bloco, StringComparison.Ordinal)))
            {
                return resposta;
            }

            atual = LinkRel(resposta, "next");
        }

        throw new InvalidOperationException($"Nenhuma página contém o bloco {bloco}.");
    }

    internal static async Task<IReadOnlyList<string>> ListarNomesDoBlocoAsync(
        HttpClient client, string colecao, string bloco)
    {
        IReadOnlyList<JsonElement> itens = await ListarDoBlocoAsync(client, colecao, bloco);
        return [.. itens.Select(i => i.GetProperty("nome").GetString()!)];
    }

    internal static async Task<IReadOnlyList<JsonElement>> ListarDoBlocoAsync(
        HttpClient client, string colecao, string bloco)
    {
        IReadOnlyList<JsonElement> todos = await ListarTudoAsync(client, colecao);
        return
        [
            .. todos.Where(i =>
                i.TryGetProperty("nome", out JsonElement nome)
                && nome.GetString()!.StartsWith(bloco, StringComparison.Ordinal)),
        ];
    }

    internal static async Task<IReadOnlyList<JsonElement>> ListarTudoAsync(HttpClient client, string colecao)
    {
        List<JsonElement> itens = [];
        string? url = $"{colecao}?limit=100";

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            itens.AddRange(await LerItensAsync(resposta));
            url = LinkRel(resposta, "next");
        }

        return itens;
    }

    internal static async Task<IReadOnlyList<JsonElement>> LerItensAsync(HttpResponseMessage resposta)
    {
        JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. documento.RootElement.EnumerateArray()];
    }

    internal static async Task<IReadOnlyList<string>> LerNomesAsync(HttpResponseMessage resposta) =>
        [.. (await LerItensAsync(resposta)).Select(i => i.GetProperty("nome").GetString()!)];

    internal static string? LinkRel(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links))
        {
            return null;
        }

        foreach (string parte in links.Single().Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!parte.Contains($"rel=\"{rel}\"", StringComparison.Ordinal))
            {
                continue;
            }

            int inicio = parte.IndexOf('<', StringComparison.Ordinal) + 1;
            int fim = parte.IndexOf('>', StringComparison.Ordinal);
            if (inicio > 0 && fim > inicio)
            {
                return parte[inicio..fim];
            }
        }

        return null;
    }

    internal static async Task<Guid> CriarCursoAsync(HttpClient client, string nome, string? codigo = null)
    {
        HttpResponseMessage resposta = await PostAdminAsync(
            client,
            "/api/configuracao/admin/cursos",
            new
            {
                codigo = codigo ?? $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
                nome,
                grau = "Bacharelado",
                nivelEnsino = "Graduação",
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    internal async Task<Guid> SemearOfertaAsync(string nomeDoCurso)
    {
        Guid curso = await SemearCursoAsync(nomeDoCurso);
        Guid local = await SemearLocalOfertaAsync();
        return await SemearOfertaDeAsync(curso, local);
    }

    internal async Task<Guid> SemearCursoAsync(string nome)
    {
        Curso curso = Curso.Criar(
            $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
            nome,
            "Bacharelado",
            "Graduação",
            null).Value!;

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        dbContext.Cursos.Add(curso);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return curso.Id;
    }

    internal async Task<Guid> SemearLocalOfertaAsync()
    {
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede,
            null,
            "1504208",
            "Marabá",
            "PA",
            ReferenciaCidadeGeo.OrigemGeoApi,
            DateTimeOffset.UtcNow,
            null,
            null).Value!;

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        dbContext.LocaisOferta.Add(local);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return local.Id;
    }

    internal async Task<Guid> SemearOfertaDeAsync(Guid cursoId, Guid localOfertaId)
    {
        OfertaCurso oferta = OfertaCurso.Criar(
            cursoId,
            localOfertaId,
            UnidadeOfertante.Criar(Guid.CreateVersion7(), "ICH", "Instituto de Ciências Humanas", "CENTRO").Value!,
            new DeclaracaoDaOfertaDeCurso($"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"])).Value!;

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        dbContext.OfertasCurso.Add(oferta);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return oferta.Id;
    }

    internal static async Task<HttpResponseMessage> PostAdminAsync(HttpClient client, string rota, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(corpo);
        return await client.SendAsync(request);
    }
}
