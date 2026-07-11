namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// A consulta unificada (ADR-0105) e a unicidade de linhagem por objeto (ADR-0107),
/// exercitadas por HTTP contra o monólito real: os atos de uma entidade em ordem
/// cronológica, a herança de vínculos pela retificação, e as recusas que impedem um
/// objeto de ser tratado por duas linhagens.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class AtosDaEntidadeEndpointTests
{
    private const string AdminAtos = "/api/publicacoes/admin/atos";
    private const string AdminTipos = "/api/publicacoes/admin/tipos-ato";
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ProcessoSeletivo = "PROCESSO_SELETIVO";

    private readonly PublicacoesEndpointFixture _fixture;

    public AtosDaEntidadeEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    // ── A consulta unificada ─────────────────────────────────────────────────

    [Fact(DisplayName = "Uma entidade acumula vários atos, devolvidos em ordem de data de publicação")]
    public async Task Listar_VariosAtos_OrdenadosPorDataDePublicacao()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        Guid chamada = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        // A chamada reúne convocação, retificação da convocação e homologação — o caso
        // que a story cita, publicado fora de ordem de propósito.
        Guid homologacao = await PostAtoIdAsync(client, PayloadAto(tipo, "20", "2026-04-10", [Vinculo(chamada)]));
        Guid convocacao = await PostAtoIdAsync(client, PayloadAto(tipo, "10", "2026-03-01", [Vinculo(chamada)]));
        Guid retificacao = await PostAtoIdAsync(
            client, PayloadRetificacao(tipo, "10", "2026-03-05", convocacao, "corrige o horário", []));

        JsonElement[] itens = await ListarDaEntidadeAsync(client, ProcessoSeletivo, chamada);

        itens.Select(i => i.GetProperty("id").GetGuid())
            .Should().Equal(convocacao, retificacao, homologacao);
    }

    [Fact(DisplayName = "A retificação herda o vínculo do ato que emenda — sem isso, sumiria da consulta")]
    public async Task Listar_Retificacao_HerdaVinculoDoRetificado()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid raiz = await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));
        // A retificação não declara vínculo algum.
        Guid retificacao = await PostAtoIdAsync(
            client, PayloadRetificacao(tipo, "13", "2026-03-14", raiz, "corrige o anexo", []));

        JsonElement[] itens = await ListarDaEntidadeAsync(client, ProcessoSeletivo, processo);

        itens.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(retificacao);
        itens.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Entidade sem ato algum devolve coleção vazia — o módulo não sabe se ela existe")]
    public async Task Listar_EntidadeSemAtos_DevolveVazio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        JsonElement[] itens = await ListarDaEntidadeAsync(client, ProcessoSeletivo, Guid.CreateVersion7());

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
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        Guid chamada = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid ato = await PostAtoIdAsync(
            client,
            PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo), Vinculo(chamada, "CHAMADA")]));

        (await ListarDaEntidadeAsync(client, ProcessoSeletivo, processo))
            .Select(i => i.GetProperty("id").GetGuid()).Should().Equal(ato);
        (await ListarDaEntidadeAsync(client, "CHAMADA", chamada))
            .Select(i => i.GetProperty("id").GetGuid()).Should().Equal(ato);
    }

    [Fact(DisplayName = "O cursor de uma entidade não navega a coleção de outra")]
    public async Task Listar_CursorDeOutraEntidade_Retorna400()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        Guid outroProcesso = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        await PostAtoIdAsync(client, PayloadAto(tipo, "1", "2026-03-01", [Vinculo(processo)]));
        await PostAtoIdAsync(client, PayloadAto(tipo, "2", "2026-03-02", [Vinculo(processo)]));

        // Página de tamanho 1 na coleção do primeiro processo: o link "next" carrega um
        // cursor escopado àquela entidade.
        HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"/api/publicacoes/entidades/{ProcessoSeletivo}/{processo}/atos?limit=1", UriKind.Relative));
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        string cursor = ExtrairCursorDoLinkNext(primeira);

        HttpResponseMessage naOutraColecao = await client.GetAsync(new Uri(
            $"/api/publicacoes/entidades/{ProcessoSeletivo}/{outroProcesso}/atos?cursor={Uri.EscapeDataString(cursor)}&direction=next",
            UriKind.Relative));

        naOutraColecao.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Cursor forjado devolve 400 — nunca 500")]
    public async Task Listar_CursorForjado_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri(
            $"/api/publicacoes/entidades/{ProcessoSeletivo}/{Guid.CreateVersion7()}/atos?cursor=nao-e-um-cursor",
            UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "A paginação percorre a coleção da entidade em ordem, sem repetir nem pular")]
    public async Task Listar_Paginado_PercorreEmOrdem()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid a = await PostAtoIdAsync(client, PayloadAto(tipo, "1", "2026-03-01", [Vinculo(processo)]));
        Guid b = await PostAtoIdAsync(client, PayloadAto(tipo, "2", "2026-03-02", [Vinculo(processo)]));
        Guid c = await PostAtoIdAsync(client, PayloadAto(tipo, "3", "2026-03-03", [Vinculo(processo)]));

        List<Guid> percorridos = [];
        string url = $"/api/publicacoes/entidades/{ProcessoSeletivo}/{processo}/atos?limit=2";

        while (true)
        {
            // O link de navegação vem absoluto (RFC 5988), o primeiro request é relativo.
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);

            using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            percorridos.AddRange(corpo.RootElement.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));

            string? proxima = LinkDe(resposta, "next");
            if (proxima is null)
            {
                break;
            }

            url = proxima;
        }

        percorridos.Should().Equal(a, b, c);
    }

    // ── Unicidade de linhagem por objeto (ADR-0107) ──────────────────────────

    [Fact(DisplayName = "Duas linhagens do mesmo tipo único no mesmo objeto: a segunda recebe 409")]
    public async Task Registrar_SegundaLinhagemNoMesmoObjeto_Retorna409()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));

        HttpResponseMessage segunda = await PostAtoAsync(
            client, PayloadAto(tipo, "14", "2026-03-14", [Vinculo(processo)]));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await segunda.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo");
    }

    [Fact(DisplayName = "A retificação da linhagem que ocupa o objeto é aceita — a vaga é da linhagem, não do ato")]
    public async Task Registrar_RetificacaoDaLinhagemQueOcupa_Retorna201()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid raiz = await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));

        HttpResponseMessage retificacao = await PostAtoAsync(
            client, PayloadRetificacao(tipo, "13", "2026-03-13", raiz, "corrige o anexo", [Vinculo(processo)]));

        retificacao.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Uma retificação de outra linhagem não invade o objeto ocupado (o furo do índice sobre a raiz)")]
    public async Task Registrar_RetificacaoDeOutraLinhagemNoObjetoOcupado_Retorna409()
    {
        // Um índice único parcial filtrado por "o ato é raiz" deixaria isto passar: a
        // retificação não é raiz e escaparia do filtro. A vaga é chaveada pela linhagem,
        // e é isso que fecha o furo.
        string tipo = await CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo)]));

        // Outra linhagem, publicada sem vínculo — passa, porque sem objeto não há vaga.
        Guid raizSemVinculo = await PostAtoIdAsync(client, PayloadAto(tipo, "99", "2026-03-20", []));

        // A retificação dela tenta vincular o objeto que a primeira linhagem já trata.
        HttpResponseMessage invasora = await PostAtoAsync(
            client,
            PayloadRetificacao(tipo, "99", "2026-03-21", raizSemVinculo, "vincula o processo", [Vinculo(processo)]));

        invasora.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await invasora.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo");
    }

    [Fact(DisplayName = "Ato publicado antes de o tipo virar único por objeto ainda bloqueia a segunda linhagem")]
    public async Task Registrar_AtoAnteriorAoAtributo_AindaBloqueia()
    {
        // O catálogo é editável: o primeiro ato não reservou vaga alguma, porque no seu
        // instante o tipo não era único por objeto. É a consulta ao histórico de atos —
        // não a tabela de vagas — que enxerga isso.
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        (string codigo, Guid tipoId) = await CriarTipoComIdAsync(unicoPorObjeto: false);
        await PostAtoIdAsync(client, PayloadAto(codigo, "13", "2026-03-13", [Vinculo(processo)]));

        await AtualizarTipoAsync(client, tipoId, codigo, unicoPorObjeto: true);

        HttpResponseMessage segunda = await PostAtoAsync(
            client, PayloadAto(codigo, "14", "2026-03-14", [Vinculo(processo)]));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await segunda.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo");
    }

    [Fact(DisplayName = "Um ato de tipo único retifica outro de tipo único distinto, e cada tipo tem a sua vaga no objeto")]
    public async Task Registrar_RetificacaoDeTipoDistinto_ReservaVagaDoSeuProprioTipo()
    {
        // A ADR-0103 admite que um tipo emende outro (um aviso retifica um edital), desde
        // que a classe de congelamento coincida. A vaga é por (objeto, tipo do ato): o
        // aviso abre a sua própria vaga no mesmo objeto, sem disputar a do edital.
        string edital = await CriarTipoAsync(unicoPorObjeto: true);
        string aviso = await CriarTipoAsync(unicoPorObjeto: true);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid raiz = await PostAtoIdAsync(client, PayloadAto(edital, "13", "2026-03-13", [Vinculo(processo)]));

        HttpResponseMessage retificacao = await PostAtoAsync(
            client, PayloadRetificacao(aviso, "50", "2026-03-20", raiz, "prorroga o prazo", []));

        retificacao.StatusCode.Should().Be(HttpStatusCode.Created);

        // Os dois atos tratam do mesmo processo: o aviso herdou o vínculo do edital.
        (await ListarDaEntidadeAsync(client, ProcessoSeletivo, processo)).Should().HaveCount(2);
    }

    [Fact(DisplayName = "Tipo único por objeto sem vínculo algum é registrável — sem objeto, não há vaga")]
    public async Task Registrar_UnicoPorObjetoSemVinculo_Retorna201()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: true);
        using HttpClient client = _fixture.Factory.CreateClient();

        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", []));
        HttpResponseMessage segundo = await PostAtoAsync(client, PayloadAto(tipo, "14", "2026-03-14", []));

        segundo.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Objetos distintos não disputam a mesma vaga")]
    public async Task Registrar_ObjetosDistintos_Retorna201()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: true);
        using HttpClient client = _fixture.Factory.CreateClient();

        await PostAtoIdAsync(client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(Guid.CreateVersion7())]));
        HttpResponseMessage outro = await PostAtoAsync(
            client, PayloadAto(tipo, "14", "2026-03-14", [Vinculo(Guid.CreateVersion7())]));

        outro.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "A mesma entidade vinculada duas vezes ao mesmo ato é recusada com 422")]
    public async Task Registrar_VinculoDuplicadoNoPayload_Retorna422()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo), Vinculo(processo)]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Elemento nulo na lista de vínculos é recusado com 422 — nunca 500")]
    public async Task Registrar_VinculoNulo_Retorna422()
    {
        // A anotação de nulidade do record não impede um `"vinculos": [null]` no corpo.
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client,
            new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                numero = "13",
                tipoCodigo = tipo,
                dataPublicacao = "2026-03-13",
                documentoHash = HashValido,
                assinante = "Jairo Belchior",
                vinculos = new object?[] { null },
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Rótulo de entidade fora da grafia canônica é recusado com 422")]
    public async Task Registrar_RotuloForaDaGrafia_Retorna422()
    {
        string tipo = await CriarTipoAsync(unicoPorObjeto: false);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client,
            PayloadAto(tipo, "13", "2026-03-13", [Vinculo(Guid.CreateVersion7(), "processo-seletivo")]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static object Vinculo(Guid entidadeId, string entidadeTipo = ProcessoSeletivo) =>
        new { entidadeTipo, entidadeId };

    private static async Task<JsonElement[]> ListarDaEntidadeAsync(HttpClient client, string tipo, Guid id)
    {
        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/publicacoes/entidades/{tipo}/{id}/atos", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    private static string ExtrairCursorDoLinkNext(HttpResponseMessage resposta)
    {
        string proxima = LinkDe(resposta, "next")
            ?? throw new InvalidOperationException("A resposta não trouxe link 'next'.");

        string query = new Uri(proxima, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(proxima).Query
            : proxima[proxima.IndexOf('?', StringComparison.Ordinal)..];

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)["cursor"]!;
    }

    private static string? LinkDe(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? valores))
        {
            return null;
        }

        foreach (string parte in string.Join(',', valores).Split(','))
        {
            if (!parte.Contains($"rel=\"{rel}\"", StringComparison.Ordinal))
            {
                continue;
            }

            int abre = parte.IndexOf('<', StringComparison.Ordinal);
            int fecha = parte.IndexOf('>', StringComparison.Ordinal);
            return parte[(abre + 1)..fecha];
        }

        return null;
    }

    private async Task<string> CriarTipoAsync(bool unicoPorObjeto)
    {
        (string codigo, _) = await CriarTipoComIdAsync(unicoPorObjeto);
        return codigo;
    }

    private async Task<(string Codigo, Guid Id)> CriarTipoComIdAsync(bool unicoPorObjeto)
    {
        // O código do tipo de ato admite só letras (UPPER_SNAKE, sem dígitos): mapeia os
        // hexadígitos do Guid para letras, mantendo a unicidade entre os testes.
        string codigo = "TIPO_" + string.Concat(
            Guid.CreateVersion7().ToString("N")[..12].Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminTipos, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            codigo,
            nome = "Tipo de teste",
            congelaConfiguracao = true,
            unicoPorObjeto,
            efeitoIrreversivel = false,
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
            baseLegal = (string?)null,
        });

        HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(
            HttpStatusCode.Created, "o corpo devolvido foi: " + await resposta.Content.ReadAsStringAsync());

        // O endpoint devolve o identificador cru, não um objeto que o envolva.
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return (codigo, corpo.RootElement.GetGuid());
    }

    private static async Task AtualizarTipoAsync(HttpClient client, Guid id, string codigo, bool unicoPorObjeto)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"{AdminTipos}/{id}", UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(new
        {
            id,
            codigo,
            nome = "Tipo de teste",
            congelaConfiguracao = true,
            unicoPorObjeto,
            efeitoIrreversivel = false,
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
            baseLegal = (string?)null,
        });

        HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static void Autenticar(HttpRequestMessage request, string role = "plataforma-admin")
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);
    }

    private static object PayloadAto(
        string tipoCodigo, string numero, string dataPublicacao, IReadOnlyList<object> vinculos) =>
        new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            numero,
            tipoCodigo,
            dataPublicacao,
            documentoHash = HashValido,
            assinante = "Jairo Belchior",
            vinculos,
        };

    private static object PayloadRetificacao(
        string tipoCodigo,
        string numero,
        string dataPublicacao,
        Guid atoRetificadoId,
        string motivo,
        IReadOnlyList<object> vinculos) =>
        new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            numero,
            tipoCodigo,
            dataPublicacao,
            documentoHash = HashValido,
            assinante = "Jairo Belchior",
            atoRetificadoId,
            motivoRetificacao = motivo,
            vinculos,
        };

    private static async Task<HttpResponseMessage> PostAtoAsync(HttpClient client, object payload)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(payload);

        return await client.SendAsync(request);
    }

    private static async Task<Guid> PostAtoIdAsync(HttpClient client, object payload)
    {
        HttpResponseMessage resposta = await PostAtoAsync(client, payload);
        resposta.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "o corpo devolvido foi: " + await resposta.Content.ReadAsStringAsync());

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return corpo.RootElement.GetProperty("atoId").GetGuid();
    }
}

