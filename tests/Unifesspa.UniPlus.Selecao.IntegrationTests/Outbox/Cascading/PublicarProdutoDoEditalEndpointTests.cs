namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A publicação de um item que o edital declara, de ponta a ponta contra o host real: o item é
/// escolhido pelo <c>Id</c> congelado, o tipo vem dele, o Publicações registra, a divulgação copia o
/// arquivo e o item passa a publicado e aparece na página pública do certame.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class PublicarProdutoDoEditalEndpointTests
{
    private readonly CascadingFixture _fixture;

    public PublicarProdutoDoEditalEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O preliminar publicado com a política confirmada vira publicado e aparece na página pública com o documento; a mesma chave de idempotência não cria dois atos")]
    public async Task Preliminar_PublicadoApareceNaPaginaPublica()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, ItensDoTeste itens) = await Preparar();
        Guid documento = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "lista.pdf");

        await AfirmarCampoPublicoPadraoAsync(client, processoId);
        Guid atoId = await PublicarERepetirComAMesmaChaveAsync(client, processoId, itens.Preliminar, documento);

        await AtoNoRegistroCentral.LerAsync(api, atoId);
        await AfirmarItemPublicadoUmaVezAsync(api, client, processoId, itens.Preliminar);
        JsonElement publico = await PublicarAtoAvulsoEndpointTests.AguardarNaPaginaPublicaAsync(client, processoId, atoId);
        publico.GetProperty("documentos").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("papel").GetString().Should().Be("principal");
    }

    private static async Task AfirmarCampoPublicoPadraoAsync(HttpClient client, Guid processoId)
    {
        JsonElement antes = await ConsultarItensAsync(client, processoId);
        antes.GetProperty("politicaDeDivulgacao").GetProperty("camposPublicos").EnumerateArray()
            .Select(static c => c.GetString()).Should().Equal("NUMERO_INSCRICAO");
    }

    private static async Task<Guid> PublicarERepetirComAMesmaChaveAsync(HttpClient client, Guid processoId, Guid produtoId, Guid documento)
    {
        string chave = Guid.CreateVersion7().ToString("N");
        Guid atoId;
        using (HttpResponseMessage resposta = await PublicarAsync(client, processoId, produtoId, documento, new OpcoesDaPublicacao(Chave: chave)))
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
            atoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        }

        using HttpResponseMessage repeticao = await PublicarAsync(client, processoId, produtoId, documento, new OpcoesDaPublicacao(Chave: chave));
        repeticao.StatusCode.Should().Be(HttpStatusCode.Created);
        (await repeticao.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid().Should().Be(atoId);
        return atoId;
    }

    private static async Task AfirmarItemPublicadoUmaVezAsync(CascadingApiFactory api, HttpClient client, Guid processoId, Guid produtoId)
    {
        JsonElement item = await ItemAsync(client, processoId, produtoId);
        item.GetProperty("situacao").GetProperty("codigo").GetString().Should().Be("PUBLICADO");
        item.GetProperty("ato").GetProperty("politicaDeDivulgacaoConfirmada").GetBoolean().Should().BeTrue();
        (await ContarAtosDoItemAsync(api, processoId, produtoId)).Should().Be(1, "a repetição devolve a resposta guardada e não registra outro ato");
    }

    [Fact(DisplayName = "As recusas têm erro nomeado: definitivo antes do preliminar, item de outra versão, resultado sem a confirmação e segundo envio do mesmo item, além da correção sem motivo")]
    public async Task Recusas_ComErroNomeado()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, ItensDoTeste itens) = await Preparar();

        await ExigirRecusaAsync(PublicarAsync(client, processoId, itens.Definitivo, await DocumentoAsync(api, processoId)), "preliminar_nao_publicado");
        await ExigirRecusaAsync(PublicarAsync(client, processoId, Guid.CreateVersion7(), await DocumentoAsync(api, processoId)), "produto_nao_declarado");
        await ExigirRecusaAsync(
            PublicarAsync(client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), new OpcoesDaPublicacao(Confirma: false)),
            "politica_de_divulgacao_nao_confirmada");
        await ExigirRecusaAsync(
            PublicarAsync(client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), new OpcoesDaPublicacao(AtoCorrigidoId: Guid.CreateVersion7())),
            "motivo_da_correcao_obrigatorio");
        await PublicarEObterOAtoAsync(api, client, processoId, itens.Preliminar);

        await ExigirRecusaAsync(PublicarAsync(client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId)), "produto_ja_publicado");
    }

    private static async Task ExigirRecusaAsync(Task<HttpResponseMessage> envio, string codigoDoErro)
    {
        using HttpResponseMessage resposta = await envio;
        await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(resposta, $"uniplus.selecao.ato_do_processo.{codigoDoErro}");
    }

    [Fact(DisplayName = "A errata do preliminar não esbarra na unicidade e vira a cabeça da cadeia; depois do definitivo o preliminar não se corrige, e o definitivo sim")]
    public async Task Errata_ECicloFechadoPeloDefinitivo()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, ItensDoTeste itens) = await Preparar();
        Guid preliminar = await PublicarEObterOAtoAsync(api, client, processoId, itens.Preliminar);
        Guid errata = await PublicarErrataDoPreliminarAsync(api, client, processoId, itens.Preliminar, preliminar);

        await AfirmarCabecaDaCadeiaAsync(api, client, processoId, itens.Preliminar, (preliminar, errata));
        Guid definitivo = await PublicarEObterOAtoAsync(api, client, processoId, itens.Definitivo);

        await ExigirRecusaAsync(
            PublicarAsync(client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), new OpcoesDaPublicacao(AtoCorrigidoId: errata, Motivo: "Tarde demais")),
            "preliminar_fechado_pelo_definitivo");
        using HttpResponseMessage corrigeDefinitivo = await PublicarAsync(
            client, processoId, itens.Definitivo, await DocumentoAsync(api, processoId), new OpcoesDaPublicacao(AtoCorrigidoId: definitivo, Motivo: "Ajuste no definitivo"));
        corrigeDefinitivo.StatusCode.Should().Be(HttpStatusCode.Created, await corrigeDefinitivo.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> PublicarErrataDoPreliminarAsync(CascadingApiFactory api, HttpClient client, Guid processoId, Guid produtoId, Guid preliminar)
    {
        using HttpResponseMessage resposta = await PublicarAsync(
            client, processoId, produtoId, await DocumentoAsync(api, processoId), new OpcoesDaPublicacao(AtoCorrigidoId: preliminar, Motivo: "Nome trocado"));
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
    }

    private static async Task AfirmarCabecaDaCadeiaAsync(
        CascadingApiFactory api, HttpClient client, Guid processoId, Guid produtoId, (Guid Preliminar, Guid Errata) atos)
    {
        await ExigirRecusaAsync(
            PublicarAsync(client, processoId, produtoId, await DocumentoAsync(api, processoId), new OpcoesDaPublicacao(Confirma: false, AtoCorrigidoId: atos.Errata, Motivo: "Outra")),
            "politica_de_divulgacao_nao_confirmada");
        await AtoNoRegistroCentral.LerAsync(api, atos.Errata);
        JsonElement item = await ItemAsync(client, processoId, produtoId);
        item.GetProperty("ato").GetProperty("id").GetGuid().Should().Be(atos.Preliminar, "a primeira publicação continua sendo a original");
        item.GetProperty("correcoes").EnumerateArray().Last().GetProperty("id").GetGuid().Should().Be(atos.Errata, "a cabeça da cadeia é a errata");
        using HttpResponseMessage porAviso = await PublicarAtoAvulsoEndpointTests.PublicarAvisoAsync(
            client, processoId, await DocumentoAsync(api, processoId), atoCorrigidoId: atos.Errata, motivo: "Contorno");
        await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(porAviso, "uniplus.selecao.ato_do_processo.ato_corrigido_eh_item_do_edital");
    }

    [Fact(DisplayName = "Quatro publicações simultâneas do mesmo item resultam em um só ato")]
    public async Task PublicacoesSimultaneas_UmaVence()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, ItensDoTeste itens) = await Preparar();
        Guid[] documentos = [.. await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DocumentoAsync(api, processoId)))];

        HttpResponseMessage[] respostas = await Task.WhenAll(documentos.Select(d => PublicarAsync(client, processoId, itens.Preliminar, d)));

        await AfirmarUmaVenceEAsOutrasRecusadasAsync(respostas);
        (await ContarAtosDoItemAsync(api, processoId, itens.Preliminar)).Should().Be(1);
    }

    private static async Task AfirmarUmaVenceEAsOutrasRecusadasAsync(HttpResponseMessage[] respostas)
    {
        respostas.Count(static r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        foreach (HttpResponseMessage perdedora in respostas.Where(static r => r.StatusCode != HttpStatusCode.Created))
        {
            perdedora.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "a corrida é recusada, e não vira erro de servidor");
            (await perdedora.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
                .Should().Be("uniplus.selecao.ato_do_processo.produto_ja_publicado");
        }

        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }

    [Fact(DisplayName = "Duas publicações simultâneas do mesmo documento em itens diferentes: uma é aceita e a outra recusada por documento já usado")]
    public async Task PublicacoesSimultaneas_MesmoDocumentoEmItensDiferentes_UmaVence()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
            api, client, [ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelDaPublicacaoPrevista.Definitivo), ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultadoDoRecurso, PapelDaPublicacaoPrevista.Definitivo)]);
        Guid[] itens = [.. (await ConsultarItensAsync(client, processoId)).GetProperty("fases").EnumerateArray()
            .SelectMany(static f => f.GetProperty("produtos").EnumerateArray()).Select(static p => p.GetProperty("id").GetGuid())];
        Guid documento = await DocumentoAsync(api, processoId);

        HttpResponseMessage[] respostas = await Task.WhenAll(itens.Select(item => PublicarAsync(client, processoId, item, documento)));

        await PublicarAtoAvulsoEndpointTests.ExigirUmaAceitaEUmaRecusadaAsync(respostas, "uniplus.selecao.ato_do_processo.documento_ja_usado_em_outro_ato");
    }

    private async Task<(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, ItensDoTeste Itens)> Preparar()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client, ProdutosDaHomologacao());

        JsonElement itens = await ConsultarItensAsync(client, processoId);
        JsonElement[] produtos = [.. itens.GetProperty("fases").EnumerateArray().SelectMany(static f => f.GetProperty("produtos").EnumerateArray())];
        return (api, client, processoId, new ItensDoTeste(
            produtos.Single(static p => p.GetProperty("papel").GetProperty("codigo").GetString() == "PRELIMINAR").GetProperty("id").GetGuid(),
            produtos.Single(static p => p.GetProperty("papel").GetProperty("codigo").GetString() == "DEFINITIVO").GetProperty("id").GetGuid()));
    }

    /// <summary>O que a fase do formulário declara publicar: o preliminar e o definitivo da homologação, cada um com o seu <c>Id</c>.</summary>
    internal static ProdutoDaFase[] ProdutosDaHomologacao() =>
    [
        ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelDaPublicacaoPrevista.Preliminar),
        ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelDaPublicacaoPrevista.Definitivo),
    ];

    private static Task<Guid> DocumentoAsync(CascadingApiFactory api, Guid processoId) =>
        PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "lista.pdf");

    private static async Task<Guid> PublicarEObterOAtoAsync(CascadingApiFactory api, HttpClient client, Guid processoId, Guid produtoId)
    {
        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, produtoId, await DocumentoAsync(api, processoId));
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        Guid atoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        await AtoNoRegistroCentral.LerAsync(api, atoId);
        return atoId;
    }

    internal static Task<HttpResponseMessage> PublicarAsync(
        HttpClient client,
        Guid processoId,
        Guid produtoId,
        Guid documentoPrincipal,
        OpcoesDaPublicacao? opcoes = null)
    {
        OpcoesDaPublicacao efetivas = opcoes ?? new OpcoesDaPublicacao();
        HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(
            HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/produtos-previstos/{produtoId}/publicacoes");
        pedido.Content = efetivas.Corpo(documentoPrincipal);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", efetivas.Chave ?? Guid.CreateVersion7().ToString("N"));
        return client.SendAsync(pedido);
    }

    internal static async Task<JsonElement> ConsultarItensAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(
            HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}/produtos-previstos");
        pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.produtos-previstos-do-processo.v1+json");
        using HttpResponseMessage resposta = await client.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> ItemAsync(HttpClient client, Guid processoId, Guid produtoId) =>
        (await ConsultarItensAsync(client, processoId)).GetProperty("fases").EnumerateArray()
            .SelectMany(static f => f.GetProperty("produtos").EnumerateArray())
            .Single(p => p.GetProperty("id").GetGuid() == produtoId);

    private static async Task<int> ContarAtosDoItemAsync(CascadingApiFactory api, Guid processoId, Guid produtoId)
    {
        string item = (await IdentidadeDoItemNoBanco.DoItemAsync(api.Services, processoId, produtoId)).Chave;
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().AtosDoProcesso.AsNoTracking()
            .CountAsync(a => a.ProcessoSeletivoId == processoId && a.ItemDoEdital == item && a.AtoCorrigidoId == null);
    }

    private sealed record ItensDoTeste(Guid Preliminar, Guid Definitivo);

    /// <summary>O que varia na publicação do item: a chave de idempotência, a confirmação da política e a correção de um ato anterior.</summary>
    internal sealed record OpcoesDaPublicacao(string? Chave = null, bool Confirma = true, Guid? AtoCorrigidoId = null, string? Motivo = null)
    {
        internal JsonContent Corpo(Guid documentoPrincipal) => JsonContent.Create(new
        {
            numero = "7/2026",
            dataDoAto = PublicarAtoAvulsoEndpointTests.HojeEmTexto(),
            assinante = "Diretor do CEPS",
            documentoPrincipalId = documentoPrincipal,
            politicaDeDivulgacaoConfirmada = Confirma,
            atoCorrigidoId = AtoCorrigidoId,
            motivoDaCorrecao = Motivo,
        });
    }
}
