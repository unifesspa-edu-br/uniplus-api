namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// O cursor da lista de inscrições vale só para o processo em que foi emitido: usado em outro processo, é recusado
/// como cursor inválido; usado no mesmo processo, continua a paginação.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CursorDasInscricoesDoProcessoEndpointTests : IAsyncLifetime
{
    private const string CursorRecusado = "uniplus.cursor.invalido";
    private const string PrimeiraPaginaComUmaInscricao = "?limit=1";

    private readonly ConcessoesDasPersonas _concessoes;
    private readonly RequisicoesAutenticadas _http;
    private readonly SemeadorDoCenarioDeAcesso _cenarios;

    public CursorDasInscricoesDoProcessoEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _concessoes = new ConcessoesDasPersonas(fixture);
        _http = new RequisicoesAutenticadas(fixture);
        _cenarios = new SemeadorDoCenarioDeAcesso(fixture);
    }

    public Task InitializeAsync() => _concessoes.RemoverDeTodasAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A primeira página com limite de uma inscrição traz o Link da página seguinte")]
    public async Task Listar_PrimeiraPaginaComLimitePequeno_TrazOLinkDaSeguinte()
    {
        (CenarioDeAcessoAosDados processoA, _) = await SemearDoisProcessosAsync(nameof(Listar_PrimeiraPaginaComLimitePequeno_TrazOLinkDaSeguinte));

        PaginaDaLista primeira = await PrimeiraPaginaAsync(processoA);

        primeira.Continuacao.Query.Should().Contain("cursor=");
    }

    [Fact(DisplayName = "O cursor emitido em um processo é recusado como cursor inválido em outro processo")]
    public async Task Listar_CursorDeOutroProcesso_ERecusado()
    {
        (CenarioDeAcessoAosDados processoA, CenarioDeAcessoAosDados processoB) =
            await SemearDoisProcessosAsync(nameof(Listar_CursorDeOutroProcesso_ERecusado));
        PaginaDaLista primeira = await PrimeiraPaginaAsync(processoA);

        using HttpResponseMessage resposta = await _http.GetAsync(
            PersonasDoAcesso.Leandro, $"{processoB.RotaDaLista()}{primeira.Continuacao.Query}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain(CursorRecusado);
    }

    [Fact(DisplayName = "O cursor emitido em um processo continua a paginação do mesmo processo")]
    public async Task Listar_CursorDoMesmoProcesso_ContinuaAPaginacao()
    {
        (CenarioDeAcessoAosDados processoA, _) = await SemearDoisProcessosAsync(nameof(Listar_CursorDoMesmoProcesso_ContinuaAPaginacao));
        PaginaDaLista primeira = await PrimeiraPaginaAsync(processoA);

        using HttpResponseMessage seguinte = await _http.GetAsync(
            PersonasDoAcesso.Leandro, $"{processoA.RotaDaLista()}{primeira.Continuacao.Query}");

        seguinte.StatusCode.Should().Be(HttpStatusCode.OK, await seguinte.Content.ReadAsStringAsync());
        (await IdsAsync(seguinte)).Should().ContainSingle().Which.Should().NotBe(primeira.Id);
    }

    private async Task<(CenarioDeAcessoAosDados ProcessoA, CenarioDeAcessoAosDados ProcessoB)> SemearDoisProcessosAsync(string nome)
    {
        CenarioDeAcessoAosDados processoA = await _cenarios.SemearAsync(nome + "-a");
        CenarioDeAcessoAosDados processoB = await _cenarios.SemearAsync(nome + "-b");
        await _concessoes.ConcederAsync(
            PersonasDoAcesso.Leandro, processoA.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);
        await _concessoes.ConcederAsync(
            PersonasDoAcesso.Leandro, processoB.Processo.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);

        return (processoA, processoB);
    }

    private async Task<PaginaDaLista> PrimeiraPaginaAsync(CenarioDeAcessoAosDados cenario)
    {
        using HttpResponseMessage resposta = await _http.GetAsync(
            PersonasDoAcesso.Leandro, $"{cenario.RotaDaLista()}{PrimeiraPaginaComUmaInscricao}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return new PaginaDaLista((await IdsAsync(resposta)).Single(), ProximaPagina(resposta));
    }

    private static Uri ProximaPagina(HttpResponseMessage resposta)
    {
        string proxima = string.Join(",", resposta.Headers.GetValues("Link")).Split(',')
            .Select(static parte => parte.Trim())
            .Single(static parte => parte.EndsWith("rel=\"next\"", StringComparison.Ordinal));
        int inicio = proxima.IndexOf('<', StringComparison.Ordinal) + 1;

        return new Uri(proxima[inicio..proxima.IndexOf('>', StringComparison.Ordinal)], UriKind.Absolute);
    }

    private static async Task<List<Guid>> IdsAsync(HttpResponseMessage resposta)
    {
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return [.. corpo.RootElement.EnumerateArray().Select(static item => item.GetProperty("id").GetGuid())];
    }

    private sealed record PaginaDaLista(Guid Id, Uri Continuacao);
}
