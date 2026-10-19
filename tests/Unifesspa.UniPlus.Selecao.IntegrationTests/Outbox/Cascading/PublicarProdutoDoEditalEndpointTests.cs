namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        string chave = Guid.CreateVersion7().ToString("N");

        JsonElement antes = await ConsultarItensAsync(client, processoId);
        antes.GetProperty("politicaDeDivulgacao").GetProperty("camposPublicos").EnumerateArray()
            .Select(static c => c.GetString()).Should().Equal("numero_inscricao");

        Guid atoId;
        using (HttpResponseMessage resposta = await PublicarAsync(client, processoId, itens.Preliminar, documento, chave))
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.Accepted, await resposta.Content.ReadAsStringAsync());
            atoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        }

        using (HttpResponseMessage repeticao = await PublicarAsync(client, processoId, itens.Preliminar, documento, chave))
        {
            repeticao.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await repeticao.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid().Should().Be(atoId);
        }

        await EsperaDeAtoRegistrado.AguardarAsync(api, atoId, "preliminar da homologação", processoId);
        await PublicarAtoAvulsoEndpointTests.AguardarSituacaoAsync(api, atoId, SituacaoDoRegistroDoAto.Registrado);

        JsonElement preliminar = await ItemAsync(client, processoId, itens.Preliminar);
        preliminar.GetProperty("situacao").GetString().Should().Be("publicado");
        preliminar.GetProperty("ato").GetProperty("produtoDoEditalId").GetGuid().Should().Be(itens.Preliminar);
        preliminar.GetProperty("ato").GetProperty("politicaDeDivulgacaoConfirmada").GetBoolean().Should().BeTrue();
        (await ContarAtosDoItemAsync(api, itens.Preliminar)).Should().Be(1, "a repetição devolve a resposta guardada e não registra outro ato");

        JsonElement publico = await PublicarAtoAvulsoEndpointTests.AguardarNaPaginaPublicaAsync(client, processoId, atoId);
        publico.GetProperty("documentos").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("papel").GetString().Should().Be("principal");
    }

    [Fact(DisplayName = "As recusas têm erro nomeado: definitivo antes do preliminar, item de outra versão, resultado sem a confirmação e segundo envio do mesmo item, além da correção sem motivo")]
    public async Task Recusas_ComErroNomeado()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, ItensDoTeste itens) = await Preparar();

        using (HttpResponseMessage definitivo = await PublicarAsync(client, processoId, itens.Definitivo, await DocumentoAsync(api, processoId)))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(definitivo, "uniplus.selecao.ato_do_processo.preliminar_nao_publicado");
        }

        using (HttpResponseMessage deOutraVersao = await PublicarAsync(client, processoId, Guid.CreateVersion7(), await DocumentoAsync(api, processoId)))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(deOutraVersao, "uniplus.selecao.ato_do_processo.produto_nao_declarado");
        }

        using (HttpResponseMessage semConfirmacao = await PublicarAsync(client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), confirma: false))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(semConfirmacao, "uniplus.selecao.ato_do_processo.politica_de_divulgacao_nao_confirmada");
        }

        using (HttpResponseMessage semMotivo = await PublicarAsync(
            client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), atoCorrigidoId: Guid.CreateVersion7()))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(semMotivo, "uniplus.selecao.ato_do_processo.motivo_da_correcao_obrigatorio");
        }

        await PublicarEAguardarAsync(api, client, processoId, itens.Preliminar);

        using (HttpResponseMessage segunda = await PublicarAsync(client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId)))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(segunda, "uniplus.selecao.ato_do_processo.produto_ja_publicado");
        }
    }

    [Fact(DisplayName = "A errata do preliminar não esbarra na unicidade e vira a cabeça da cadeia; depois do definitivo o preliminar não se corrige, e o definitivo sim")]
    public async Task Errata_ECicloFechadoPeloDefinitivo()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, ItensDoTeste itens) = await Preparar();
        Guid preliminar = await PublicarEAguardarAsync(api, client, processoId, itens.Preliminar);

        Guid errata;
        using (HttpResponseMessage resposta = await PublicarAsync(
            client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), atoCorrigidoId: preliminar, motivo: "Nome trocado"))
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.Accepted, await resposta.Content.ReadAsStringAsync());
            errata = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        }

        using (HttpResponseMessage semConfirmacao = await PublicarAsync(
            client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), confirma: false, atoCorrigidoId: errata, motivo: "Outra"))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(semConfirmacao, "uniplus.selecao.ato_do_processo.politica_de_divulgacao_nao_confirmada");
        }

        await EsperaDeAtoRegistrado.AguardarAsync(api, errata, "errata do preliminar", processoId, preliminar);
        JsonElement item = await ItemAsync(client, processoId, itens.Preliminar);
        item.GetProperty("ato").GetProperty("id").GetGuid().Should().Be(preliminar, "a primeira publicação continua sendo a original");
        item.GetProperty("correcoes").EnumerateArray().Last().GetProperty("id").GetGuid().Should().Be(errata, "a cabeça da cadeia é a errata");

        using (HttpResponseMessage porAviso = await PublicarAtoAvulsoEndpointTests.PublicarAvisoAsync(
            client, processoId, await DocumentoAsync(api, processoId), atoCorrigidoId: errata, motivo: "Contorno"))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(porAviso, "uniplus.selecao.ato_do_processo.ato_corrigido_eh_item_do_edital");
        }

        Guid definitivo = await PublicarEAguardarAsync(api, client, processoId, itens.Definitivo);

        using (HttpResponseMessage corrigePreliminar = await PublicarAsync(
            client, processoId, itens.Preliminar, await DocumentoAsync(api, processoId), atoCorrigidoId: errata, motivo: "Tarde demais"))
        {
            await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(corrigePreliminar, "uniplus.selecao.ato_do_processo.preliminar_fechado_pelo_definitivo");
        }

        using HttpResponseMessage corrigeDefinitivo = await PublicarAsync(
            client, processoId, itens.Definitivo, await DocumentoAsync(api, processoId), atoCorrigidoId: definitivo, motivo: "Ajuste no definitivo");
        corrigeDefinitivo.StatusCode.Should().Be(HttpStatusCode.Accepted, await corrigeDefinitivo.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Quatro publicações simultâneas do mesmo item resultam em um só ato")]
    public async Task PublicacoesSimultaneas_UmaVence()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, ItensDoTeste itens) = await Preparar();
        Guid[] documentos = [.. await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DocumentoAsync(api, processoId)))];

        HttpResponseMessage[] respostas = await Task.WhenAll(documentos.Select(d => PublicarAsync(client, processoId, itens.Preliminar, d)));

        respostas.Count(static r => r.StatusCode == HttpStatusCode.Accepted).Should().Be(1);
        foreach (HttpResponseMessage perdedora in respostas.Where(static r => r.StatusCode != HttpStatusCode.Accepted))
        {
            perdedora.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "a corrida é recusada, e não vira erro de servidor");
            (await perdedora.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
                .Should().BeOneOf("uniplus.selecao.ato_do_processo.produto_ja_publicado", "uniplus.selecao.ato_do_processo.produto_aguarda_registro");
        }

        (await ContarAtosDoItemAsync(api, itens.Preliminar)).Should().Be(1);
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
            api, client, [ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelProdutoFase.Definitivo), ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultadoDoRecurso, PapelProdutoFase.Definitivo)]);
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
            produtos.Single(static p => p.GetProperty("papel").GetString() == "PRELIMINAR").GetProperty("id").GetGuid(),
            produtos.Single(static p => p.GetProperty("papel").GetString() == "DEFINITIVO").GetProperty("id").GetGuid()));
    }

    /// <summary>O que a fase do formulário declara publicar: o preliminar e o definitivo da homologação, cada um com o seu <c>Id</c>.</summary>
    private static ProdutoDaFase[] ProdutosDaHomologacao() =>
    [
        ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelProdutoFase.Preliminar),
        ProdutoDaFase.Criar(DadosDoAtoDeTeste.TipoResultado, PapelProdutoFase.Definitivo),
    ];

    private static Task<Guid> DocumentoAsync(CascadingApiFactory api, Guid processoId) =>
        PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "lista.pdf");

    private static async Task<Guid> PublicarEAguardarAsync(CascadingApiFactory api, HttpClient client, Guid processoId, Guid produtoId)
    {
        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, produtoId, await DocumentoAsync(api, processoId));
        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted, await resposta.Content.ReadAsStringAsync());
        Guid atoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        await EsperaDeAtoRegistrado.AguardarAsync(api, atoId, "item do edital", processoId);
        await PublicarAtoAvulsoEndpointTests.AguardarSituacaoAsync(api, atoId, SituacaoDoRegistroDoAto.Registrado);
        return atoId;
    }

    private static Task<HttpResponseMessage> PublicarAsync(
        HttpClient client,
        Guid processoId,
        Guid produtoId,
        Guid documentoPrincipal,
        string? chave = null,
        bool confirma = true,
        Guid? atoCorrigidoId = null,
        string? motivo = null)
    {
        HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(
            HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/produtos-previstos/{produtoId}/publicacoes");
        pedido.Content = JsonContent.Create(new
        {
            numero = "7/2026",
            dataPublicacao = PublicarAtoAvulsoEndpointTests.HojeEmTexto(),
            assinante = "Diretor do CEPS",
            documentoPrincipalId = documentoPrincipal,
            politicaDeDivulgacaoConfirmada = confirma,
            atoCorrigidoId,
            motivoDaCorrecao = motivo,
        });
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave ?? Guid.CreateVersion7().ToString("N"));
        return client.SendAsync(pedido);
    }

    private static async Task<JsonElement> ConsultarItensAsync(HttpClient client, Guid processoId)
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

    private static async Task<int> ContarAtosDoItemAsync(CascadingApiFactory api, Guid produtoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().AtosDoProcesso.AsNoTracking()
            .CountAsync(a => a.ProdutoDoEditalId == produtoId && a.AtoCorrigidoId == null);
    }

    private sealed record ItensDoTeste(Guid Preliminar, Guid Definitivo);
}
