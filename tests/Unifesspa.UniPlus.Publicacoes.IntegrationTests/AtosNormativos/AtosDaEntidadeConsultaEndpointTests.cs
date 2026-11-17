namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static AtoNormativoApoio;
using static AtosDaEntidadeApoio;

/// <summary>
/// A consulta unificada (ADR-0105), exercitada por HTTP contra o monólito real: os atos de uma entidade em
/// ordem cronológica, a herança de vínculos pela retificação, o escopo do cursor e a paginação.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class AtosDaEntidadeConsultaEndpointTests
{
    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtosDaEntidadeApoio _apoio;

    public AtosDaEntidadeConsultaEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtosDaEntidadeApoio(_fixture);
    }

    /// <remarks>
    /// A chamada reúne convocação, retificação da convocação e homologação. A homologação declara a data
    /// mais tardia e é divulgada primeiro: a ordem é a da divulgação.
    /// </remarks>
    [Fact(DisplayName = "Uma entidade acumula vários atos, devolvidos na ordem em que foram divulgados, e não na da data do ato")]
    public async Task Listar_VariosAtos_OrdenadosPeloInstanteDaDivulgacao()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid chamada = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid homologacao = await PostAtoIdAsync(client, PayloadAto(tipo, "20", "2026-04-10", [Vinculo(chamada)]));
        Guid convocacao = await PostAtoIdAsync(client, PayloadAto(tipo, "10", "2026-03-01", [Vinculo(chamada)]));
        Guid retificacao = await PostAtoIdAsync(
            client, PayloadRetificacao(tipo, "10", "2026-03-05", convocacao, "corrige o horário", []));

        (await ListarIdsDaEntidadeAsync(client, ObjetoA, chamada)).Should().Equal(homologacao, convocacao, retificacao);
    }

    /// <remarks>A retificação não declara vínculo algum.</remarks>
    [Fact(DisplayName = "A retificação herda o vínculo do ato que emenda — sem isso, sumiria da consulta")]
    public async Task Listar_Retificacao_HerdaVinculoDoRetificado()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid raiz = await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));
        Guid retificacao = await PostAtoIdAsync(
            client, PayloadRetificacao(tipo, "13", "2026-03-14", raiz, "corrige o anexo", []));

        Guid[] ids = await ListarIdsDaEntidadeAsync(client, ObjetoA, processo);

        ids.Should().Contain(retificacao);
        ids.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Entidade sem ato algum devolve coleção vazia — o módulo não sabe se ela existe")]
    public async Task Listar_EntidadeSemAtos_DevolveVazio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        JsonElement[] itens = await ListarDaEntidadeAsync(client, ObjetoA, Guid.CreateVersion7());

        itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo de entidade fora da grafia canônica devolve 400 — e não uma coleção vazia")]
    public async Task Listar_TipoForaDaGrafiaCanonica_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/publicacoes/entidades/processo-seletivo/{Guid.CreateVersion7()}/atos", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Um ato vincula-se a mais de uma entidade, e aparece na consulta das duas")]
    public async Task Listar_AtoComDoisVinculos_ApareceNasDuasConsultas()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        Guid chamada = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid ato = await PostAtoIdAsync(
            client,
            PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo), Vinculo(chamada, "CHAMADA")]));

        (await ListarIdsDaEntidadeAsync(client, ObjetoA, processo)).Should().Equal(ato);
        (await ListarIdsDaEntidadeAsync(client, "CHAMADA", chamada)).Should().Equal(ato);
    }

    /// <remarks>
    /// A página de tamanho 1 na coleção do primeiro processo devolve um link "next" com um cursor escopado
    /// àquela entidade.
    /// </remarks>
    [Fact(DisplayName = "O cursor de uma entidade não navega a coleção de outra")]
    public async Task Listar_CursorDeOutraEntidade_Retorna400()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        await PostAtoIdAsync(client, PayloadAto(tipo, "1", "2026-03-01", [Vinculo(processo)]));
        await PostAtoIdAsync(client, PayloadAto(tipo, "2", "2026-03-02", [Vinculo(processo)]));
        string cursor = await CursorDaPrimeiraPaginaAsync(client, processo);

        HttpResponseMessage naOutraColecao = await client.GetAsync(new Uri(
            $"/api/publicacoes/entidades/{ObjetoA}/{Guid.CreateVersion7()}/atos?cursor={cursor}&direction=next",
            UriKind.Relative));

        naOutraColecao.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Cursor forjado devolve 400 — nunca 500")]
    public async Task Listar_CursorForjado_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri(
            $"/api/publicacoes/entidades/{ObjetoA}/{Guid.CreateVersion7()}/atos?cursor=nao-e-um-cursor",
            UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <remarks>O link de navegação vem absoluto (RFC 5988); o primeiro request é relativo.</remarks>
    [Fact(DisplayName = "A paginação percorre a coleção da entidade em ordem, sem repetir nem pular")]
    public async Task Listar_Paginado_PercorreEmOrdem()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid a = await PostAtoIdAsync(client, PayloadAto(tipo, "1", "2026-03-01", [Vinculo(processo)]));
        Guid b = await PostAtoIdAsync(client, PayloadAto(tipo, "2", "2026-03-02", [Vinculo(processo)]));
        Guid c = await PostAtoIdAsync(client, PayloadAto(tipo, "3", "2026-03-03", [Vinculo(processo)]));

        List<Guid> percorridos = await PercorrerAsPaginasAsync(client, $"/api/publicacoes/entidades/{ObjetoA}/{processo}/atos?limit=2");

        percorridos.Should().Equal(a, b, c);
    }

    [Fact(DisplayName = "A consulta do certame, que o Portal consome, devolve o ato registrado pelo domínio")]
    public async Task Listar_ProcessoSeletivoRegistradoPeloDominio_DevolveOAto()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        Guid ato = await _apoio.RegistrarPeloDominioAsync(tipo, "13", processo);
        using HttpClient client = _fixture.Factory.CreateClient();

        (await ListarIdsDaEntidadeAsync(client, EntidadesDeRegistroPeloDominio.ProcessoSeletivo, processo)).Should().Equal(ato);
    }

    private static async Task<string> CursorDaPrimeiraPaginaAsync(HttpClient client, Guid processo)
    {
        HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"/api/publicacoes/entidades/{ObjetoA}/{processo}/atos?limit=1", UriKind.Relative));
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);

        return Uri.EscapeDataString(ExtrairCursorDoLinkNext(primeira));
    }

    private static async Task<List<Guid>> PercorrerAsPaginasAsync(HttpClient client, string primeiraUrl)
    {
        List<Guid> percorridos = [];
        string? url = primeiraUrl;

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);

            using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            percorridos.AddRange(corpo.RootElement.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
            url = LinkDe(resposta, "next");
        }

        return percorridos;
    }
}
