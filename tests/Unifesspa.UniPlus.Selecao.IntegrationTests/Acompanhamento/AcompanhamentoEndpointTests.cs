namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Acompanhamento;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static AcompanhamentoAutorizacaoTests;

/// <summary>
/// As consultas de acompanhamento com a validação temporária habilitada, sobre processos
/// persistidos: identificação mínima do processo, navegação pelos links de paginação e 404 do
/// processo inexistente.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AcompanhamentoEndpointTests
{
    private const string Colecao = "/api/selecao/acompanhamento/processos-seletivos";
    private const string MediaType = "application/vnd.uniplus.acompanhamento-processo-seletivo.v1+json";

    private readonly CascadingFixture _fixture;

    public AcompanhamentoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O detalhe devolve só id, nome e status do processo persistido")]
    public async Task ObterPorId_DevolveIdentificacaoMinima()
    {
        ProcessoSeletivo processo = await SemearAsync(nameof(ObterPorId_DevolveIdentificacaoMinima));
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_fixture.Factory);
        using HttpClient client = ClienteAutenticado(habilitada);

        using HttpResponseMessage resposta = await client.GetAsync($"{Colecao}/{processo.Id:D}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(MediaType);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "nome", "status"]);
        corpo.RootElement.GetProperty("id").GetGuid().Should().Be(processo.Id);
        corpo.RootElement.GetProperty("nome").GetString().Should().Be(processo.Nome);
        corpo.RootElement.GetProperty("status").GetString().Should().Be("rascunho");
    }

    [Fact(DisplayName = "Processo inexistente responde 404")]
    public async Task ObterPorId_Inexistente_404()
    {
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_fixture.Factory);
        using HttpClient client = ClienteAutenticado(habilitada);

        using HttpResponseMessage resposta = await client.GetAsync($"{Colecao}/{Guid.CreateVersion7():D}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "rel=\"next\" avança para outro processo e rel=\"prev\" volta à página anterior")]
    public async Task Listar_NavegaNosDoisSentidos()
    {
        await SemearAsync($"{nameof(Listar_NavegaNosDoisSentidos)} A");
        await SemearAsync($"{nameof(Listar_NavegaNosDoisSentidos)} B");
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_fixture.Factory);
        using HttpClient client = ClienteAutenticado(habilitada);

        using HttpResponseMessage primeira = await client.GetAsync($"{Colecao}?limit=1");
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        primeira.Headers.GetValues("X-Page-Size").Should().Equal("1");
        IReadOnlyList<Guid> idsPrimeira = await LerIdsAsync(primeira);
        idsPrimeira.Should().ContainSingle();
        LinkRel(primeira, "self").Should().NotBeNull();
        LinkRel(primeira, "prev").Should().BeNull("a primeira página não tem anterior");

        using HttpResponseMessage segunda = await client.GetAsync(
            new Uri(LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!, UriKind.RelativeOrAbsolute));
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<Guid> idsSegunda = await LerIdsAsync(segunda);
        idsSegunda.Should().ContainSingle().Which.Should().NotBe(idsPrimeira[0]);

        using HttpResponseMessage volta = await client.GetAsync(
            new Uri(LinkRel(segunda, "prev").Should().NotBeNull().And.Subject.ToString()!, UriKind.RelativeOrAbsolute));
        (await LerIdsAsync(volta)).Should().Equal(idsPrimeira);
    }

    private async Task<ProcessoSeletivo> SemearAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return processo;
    }

    private static async Task<IReadOnlyList<Guid>> LerIdsAsync(HttpResponseMessage resposta)
    {
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private static string? LinkRel(HttpResponseMessage resposta, string rel)
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
}
