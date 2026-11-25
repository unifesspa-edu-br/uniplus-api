namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoFormularioPeloEndpoint;

/// <summary>
/// A leitura pública do formulário por finalidade (UNI-REQ-0144), fim a fim pelo HTTP, sob
/// <c>/processos-seletivos/{id}/formularios/{finalidade}</c> (ADR-0064).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FormulariosLeituraPublicaEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FormulariosLeituraPublicaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "GET público de processo em rascunho responde o MESMO que de processo inexistente")]
    public async Task Obter_ProcessoEmRascunho_RespondeIgualAoInexistente()
    {
        // A apresentação é anônima. Enquanto ela distinguia rascunho (422) de inexistente (404),
        // respondia a um estranho, numa requisição, se um identificador corresponde a um processo
        // que ainda não é público. Resolvendo pela divulgação, os dois deixam de ser distinguíveis
        // — nenhum dos dois tem linha.
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Obter_ProcessoEmRascunho_RespondeIgualAoInexistente));

        HttpResponseMessage emRascunho = await ctx.GetFormularioAsync();
        HttpResponseMessage inexistente = await ctx.Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{Guid.CreateVersion7()}/formularios/INSCRICAO", UriKind.Relative));

        emRascunho.StatusCode.Should().Be(HttpStatusCode.NotFound);
        emRascunho.StatusCode.Should().Be(
            inexistente.StatusCode, "a resposta não pode separar o que ainda não é público do que não existe");
    }

    [Fact(DisplayName = "GET público, sem autenticação, retorna 404 para processo inexistente")]
    public async Task Obter_ProcessoInexistente_404()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Obter_ProcessoInexistente_404));

        HttpResponseMessage resposta = await ctx.Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{Guid.NewGuid()}/formularios/INSCRICAO", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET público, sem autenticação, retorna 200 com título/termo/fatos para processo publicado")]
    public async Task Obter_ProcessoPublicado_200ComFormularioEFatos()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Obter_ProcessoPublicado_200ComFormularioEFatos));

        (await ctx.PutFormularioAsync("Formulário de Inscrição"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.PutFatosAsync([])).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ctx.PublicarEEsperarDivulgacaoAsync();

        HttpResponseMessage resposta = await ctx.GetFormularioAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        ConferirCabecalhoDoFormularioPublicado(doc.RootElement);
        ConferirCorRacaDoFormularioPublicado(doc.RootElement);
    }

    private static void ConferirCabecalhoDoFormularioPublicado(JsonElement raiz)
    {
        JsonElement root = raiz.GetProperty("formulario");
        root.GetProperty("finalidade").GetString().Should().Be("INSCRICAO");
        root.GetProperty("titulo").GetString().Should().Be("Formulário de Inscrição");
        root.GetProperty("paginas").EnumerateArray().Select(static e => e.GetProperty("codigo").GetString())
            .Should().Equal(ConjuntoBasicoDaInscricao.CodigoDaSecao, "DADOS", "REVISAO");
        root.GetProperty("termos").GetArrayLength().Should().Be(0);
        root.GetProperty("pressupostos").GetProperty("fatos").GetArrayLength().Should().Be(0);
    }

    private static void ConferirCorRacaDoFormularioPublicado(JsonElement raiz)
    {
        JsonElement root = raiz.GetProperty("formulario");
        JsonElement fatos = root.GetProperty("coleta").GetProperty("itens");
        fatos.GetArrayLength().Should().Be(ConjuntoBasicoDaInscricao.Itens.Count);
        JsonElement fato = fatos.EnumerateArray().Single(static f => f.GetProperty("fatoCodigo").GetString() == "COR_RACA");
        fato.GetProperty("fatoCodigo").GetString().Should().Be("COR_RACA");
        JsonElement apresentacao = fato.GetProperty("apresentacao");
        apresentacao.GetProperty("rotulo").GetString().Should().Be("Cor ou raça");
        apresentacao.GetProperty("tipoDeCampo").GetString().Should().Be("ESCOLHA_UNICA");
        fato.GetProperty("paginaCodigo").GetString().Should().Be(ConjuntoBasicoDaInscricao.CodigoDaSecao);
        fato.TryGetProperty("obrigatoriedade", out _).Should().BeFalse("as regras do campo estão só nas regras do formulário");
        ConferirRegraDaCorRaca(root);
    }

    private static void ConferirRegraDaCorRaca(JsonElement root)
    {
        JsonElement regraDoCampo = root.GetProperty("regras").GetProperty("paginas").EnumerateArray()
            .SelectMany(static e => e.GetProperty("itens").EnumerateArray())
            .Single(static i => i.GetProperty("fatoCodigo").GetString() == "COR_RACA");
        regraDoCampo.GetProperty("regras").GetProperty("obrigatoriedade").GetProperty("tipo").GetString().Should().Be("SEMPRE");
        regraDoCampo.GetProperty("oferta").GetArrayLength().Should().BePositive("a resposta fora da oferta congelada não vale");
    }

    /// <summary>
    /// O 404 anterior à divulgação é transitório: o mesmo endereço passa a servir o formulário assim que a materialização
    /// chega. Sem diretiva, um cache compartilhado atribui frescor heurístico à recusa e segue escondendo o formulário já público.
    /// </summary>
    [Fact(DisplayName = "GET público exige revalidação, inclusive na recusa anterior à divulgação")]
    public async Task Obter_AntesEDepoisDaDivulgacao_ExigeRevalidacao()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Obter_AntesEDepoisDaDivulgacao_ExigeRevalidacao));
        (await ctx.PutFormularioAsync("Formulário de Inscrição")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage recusa = await ctx.GetFormularioAsync();
        recusa.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ExigeRevalidacao(recusa, "a recusa é transitória e o endereço passa a servir o formulário quando a divulgação chega");

        await ctx.PublicarEEsperarDivulgacaoAsync();
        HttpResponseMessage servido = await ctx.GetFormularioAsync();
        servido.StatusCode.Should().Be(HttpStatusCode.OK);
        ExigeRevalidacao(servido, "o endereço também não muda quando uma retificação troca a versão servida");
    }

    private static void ExigeRevalidacao(HttpResponseMessage resposta, string motivo) =>
        resposta.Headers.CacheControl!.NoCache.Should().BeTrue(motivo);
}
