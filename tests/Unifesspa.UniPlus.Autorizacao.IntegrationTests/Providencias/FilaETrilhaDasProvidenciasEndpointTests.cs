namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Providencias;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A fila das providências em ordem de abertura, pelo instante gravado e não pelo identificador, nos dois sentidos do
/// cursor (UNI-REQ-0375), e o registro da leitura de cada providência exposta na trilha da concessão (ADR-0153).
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class FilaETrilhaDasProvidenciasEndpointTests : ProvidenciaEndpointTestBase
{
    public FilaETrilhaDasProvidenciasEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Providências com a abertura fora da ordem dos identificadores saem pela abertura, para a frente e para trás")]
    public async Task Fila_PelaAbertura_NosDoisSentidos()
    {
        Guid[] porId = await AbertasComAAberturaInvertidaAsync();
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        Pagina primeira = await PaginaAsync(diogo, new Uri($"{RotaDasProvidencias}?limit=1", UriKind.Relative));
        Pagina segunda = await PaginaAsync(diogo, primeira.Proxima!);
        Pagina terceira = await PaginaAsync(diogo, segunda.Proxima!);
        Pagina voltaASegunda = await PaginaAsync(diogo, terceira.Anterior!);
        Pagina voltaAPrimeira = await PaginaAsync(diogo, voltaASegunda.Anterior!);

        new[] { primeira, segunda, terceira }.SelectMany(static p => p.Ids).Should().Equal(porId[2], porId[1], porId[0]);
        new[] { voltaASegunda, voltaAPrimeira }.SelectMany(static p => p.Ids).Should().Equal(porId[1], porId[2]);
        voltaAPrimeira.Anterior.Should().BeNull();
    }

    [Fact(DisplayName = "A trilha da concessão registra a leitura de cada providência exposta, pela permissão de quem lê")]
    public async Task Trilha_RegistraALeituraDaProvidencia()
    {
        (Guid concessao, Guid providencia) = await ProvidenciaAbertaAsync();
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        int antes = Registro.Registrados.Count;

        using JsonDocument trilha = await LerAsync(diogo, $"{Rota}/{concessao}/trilha");

        trilha.RootElement.EnumerateArray().Select(static e => e.GetProperty("tipo").GetString()).Should().Contain("providencia_solicitada");
        Registro.Registrados.Skip(antes).Should().Contain(r =>
            r.ResourceTipo == "providencia-sobre-concessao" && r.ResourceId == providencia && r.AtorSubject == Diogo.Sub && r.Permissao == Administrar);
    }

    // Três providências abertas, com a abertura recuada para antes de qualquer outra da base e invertida em relação aos
    // identificadores: a mais nova pelo identificador é a mais antiga pela abertura. Devolve os identificadores em ordem.
    private async Task<Guid[]> AbertasComAAberturaInvertidaAsync()
    {
        Guid[] ids = [(await ProvidenciaAbertaAsync()).ProvidenciaId, (await ProvidenciaAbertaAsync()).ProvidenciaId, (await ProvidenciaAbertaAsync()).ProvidenciaId];
        for (int i = 0; i < ids.Length; i++)
        {
            await ExecutarSqlAsync(
                "UPDATE autorizacao.providencias SET aberta_em = {0} WHERE id = {1}", new DateTimeOffset(2001, 1, 3 - i, 12, 0, 0, TimeSpan.Zero), ids[i]);
        }

        return [.. ids.Order()];
    }

    private static async Task<Pagina> PaginaAsync(HttpClient cliente, Uri endereco)
    {
        using HttpResponseMessage resposta = await cliente.GetAsync(endereco);
        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.OK);
        JsonElement itens = corpo.ValueKind == JsonValueKind.Array ? corpo : corpo.GetProperty("items");
        string[] links = resposta.Headers.TryGetValues("Link", out IEnumerable<string>? valores) ? [.. valores.SelectMany(static l => l.Split(','))] : [];

        return new([.. itens.EnumerateArray().Select(static i => i.GetProperty("id").GetGuid())], Link(links, "next"), Link(links, "prev"));
    }

    private static Uri? Link(string[] links, string rel) =>
        links.Select(static l => l.Trim())
            .Where(l => l.EndsWith($"rel=\"{rel}\"", StringComparison.Ordinal))
            .Select(static l => new Uri(l[(l.IndexOf('<', StringComparison.Ordinal) + 1)..l.IndexOf('>', StringComparison.Ordinal)], UriKind.RelativeOrAbsolute))
            .FirstOrDefault();

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();

    private sealed record Pagina(Guid[] Ids, Uri? Proxima, Uri? Anterior);
}
