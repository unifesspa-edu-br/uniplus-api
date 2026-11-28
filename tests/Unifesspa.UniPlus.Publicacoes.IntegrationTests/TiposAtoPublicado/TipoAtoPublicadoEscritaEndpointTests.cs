namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static TipoAtoPublicadoApoio;

/// <summary>
/// Escrita dos endpoints de <c>TipoAtoPublicado</c> contra o monólito real com Postgres
/// efêmero: autenticação/autorização, idempotência, conflito de vigência (409) e
/// invariantes de payload (400/422).
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class TipoAtoPublicadoEscritaEndpointTests
{
    private readonly PublicacoesEndpointFixture _fixture;
    private readonly TipoAtoPublicadoApoio _apoio;

    public TipoAtoPublicadoEscritaEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new TipoAtoPublicadoApoio(fixture);
    }

    // ── Autorização ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST admin sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "DELETE admin sem autenticação retorna 401")]
    public async Task Remover_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"{Admin}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Idempotência ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(new CorpoDoTipoDeAto(CodigoUnico()));

        HttpResponseMessage response = await client.SendAsync(request);

        // Prova que AddIdempotency está registrado: sem ele o filtro não existe e a
        // requisição alcançaria o handler, devolvendo 201.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST repetido com a mesma chave e o mesmo corpo devolve a resposta cacheada")]
    public async Task Criar_ReplayLegitimo_DevolveCache()
    {
        string chave = Guid.NewGuid().ToString();
        CorpoDoTipoDeAto corpo = new(CodigoUnico());

        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage primeira = await PostAsync(client, corpo, chave);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        string corpoPrimeira = await primeira.Content.ReadAsStringAsync();

        HttpResponseMessage segunda = await PostAsync(client, corpo, chave);
        segunda.StatusCode.Should().Be(HttpStatusCode.Created);
        (await segunda.Content.ReadAsStringAsync()).Should().Be(corpoPrimeira);
    }

    [Fact(DisplayName = "POST com a mesma chave e corpo diferente retorna 422 body_mismatch")]
    public async Task Criar_MesmaChaveCorpoDiferente_Retorna422()
    {
        string chave = Guid.NewGuid().ToString();

        using HttpClient client = _fixture.Factory.CreateClient();

        (await PostAsync(client, new CorpoDoTipoDeAto(CodigoUnico()), chave)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segunda = await PostAsync(client, new CorpoDoTipoDeAto(CodigoUnico()), chave);

        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.idempotency.body_mismatch");
    }

    // ── Escrita ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST cria e devolve 201 com Location apontando para o recurso")]
    public async Task Criar_Retorna201ComLocation()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(client, new CorpoDoTipoDeAto(CodigoUnico()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().StartWith($"{Base}/");
    }

    [Fact(DisplayName = "POST grava os atributos do documento avulso e o GET os devolve")]
    public async Task Criar_GravaOsAtributosDoDocumentoAvulso()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        CorpoDoTipoDeAto corpo = new(CodigoUnico())
        {
            CongelaConfiguracao = false,
            PublicavelComoAvulso = true,
            ExigeAtoCorrigido = true,
        };

        HttpResponseMessage criado = await PostAsync(client, corpo);
        criado.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument doc = await LerAsync(client, criado.Headers.Location!);
        doc.RootElement.GetProperty("publicavelComoAvulso").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("exigeAtoCorrigido").GetBoolean().Should().BeTrue();
    }

    [Theory(DisplayName = "POST sem um campo obrigatório é recusado com 422 que o nomeia, não com 400")]
    [MemberData(nameof(CamposObrigatorios))]
    public async Task Criar_SemCampoObrigatorio_Retorna422(string campo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(client, new CorpoDoTipoDeAto(CodigoUnico()).Sem(campo));

        await ConferirRecusaDeCampoObrigatorio(response, campo);
    }

    [Theory(DisplayName = "POST com um campo obrigatório nulo é recusado com 422, igual ao ausente")]
    [MemberData(nameof(CamposObrigatorios))]
    public async Task Criar_ComCampoObrigatorioNulo_Retorna422(string campo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(client, new CorpoDoTipoDeAto(CodigoUnico()).ComNulo(campo));

        await ConferirRecusaDeCampoObrigatorio(response, campo);
    }

    [Theory(DisplayName = "PUT sem um campo obrigatório é recusado com 422 e não o zera em silêncio")]
    [MemberData(nameof(CamposObrigatorios))]
    public async Task Atualizar_SemCampoObrigatorio_Retorna422(string campo)
    {
        string codigo = CodigoUnico();
        Guid id = await _apoio.CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await AtualizarAsync(client, id, new CorpoDoTipoDeAto(codigo) { Id = id }.Sem(campo));

        await ConferirRecusaDeCampoObrigatorio(response, campo);
    }

    [Fact(DisplayName = "POST sem os atributos e com nome curto acumula as recusas, na ordem do contrato")]
    public async Task Criar_SemAtributosEComNomeCurto_AcumulaNaOrdemDoContrato()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        CorpoDoTipoDeAto corpo = new CorpoDoTipoDeAto(CodigoUnico()) { Nome = "E" }.SemOsAtributosDoDocumentoAvulso().Sem("vigenciaInicio");

        HttpResponseMessage response = await PostAsync(client, corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .Should().Equal("nome", "publicavelComoAvulso", "exigeAtoCorrigido", "vigenciaInicio");
    }

    public static TheoryData<string> CamposObrigatorios => new()
    {
        "congelaConfiguracao",
        "unicoPorObjeto",
        "efeitoIrreversivel",
        "ehResultado",
        "publicavelComoAvulso",
        "exigeAtoCorrigido",
        "vigenciaInicio",
    };

    private static async Task ConferirRecusaDeCampoObrigatorio(HttpResponseMessage response, string campo)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erro = problema.RootElement.GetProperty("errors")[0];
        erro.GetProperty("field").GetString().Should().Be(campo);
        erro.GetProperty("code").GetString().Should().Be("uniplus.validacao.obrigatorio");
    }

    [Fact(DisplayName = "POST com janela sobreposta retorna 409 com o type do ProblemDetails")]
    public async Task Criar_ComSobreposicao_Retorna409()
    {
        string codigo = CodigoUnico();
        await _apoio.CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAsync(client, new CorpoDoTipoDeAto(codigo));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.tipo_ato.vigencia_sobreposta");
    }

    [Fact(DisplayName = "POST com janela adjacente é aceito — o fim é exclusivo")]
    public async Task Criar_ComJanelaAdjacente_Retorna201()
    {
        string codigo = CodigoUnico();
        await _apoio.CriarAsync(codigo, inicio: "2026-01-01", fim: "2026-06-01");

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAsync(
            client, new CorpoDoTipoDeAto(codigo) { VigenciaInicio = "2026-06-01" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "POST com código minúsculo retorna 422 — validação de domínio")]
    public async Task Criar_ComCodigoMinusculo_Retorna422()
    {
        // ADR-0125: o formato do código é validado pelo agregado, não mais por um
        // FluentValidation à parte — o GlobalExceptionMiddleware traduz o DomainError
        // (TipoAtoPublicadoErrorCodes.CodigoFormato) para o mapeamento registrado.
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(client, new CorpoDoTipoDeAto("edital_abertura"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("uniplus.publicacoes.tipo_ato.codigo_formato");
    }

    [Fact(DisplayName = "POST de tipo que é resultado e de efeito irreversível retorna 422 com o código próprio")]
    public async Task Criar_ResultadoComEfeitoIrreversivel_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        CorpoDoTipoDeAto corpo = new(CodigoUnico())
        {
            EfeitoIrreversivel = true,
            EhResultado = true,
            CongelaConfiguracao = false,
        };

        HttpResponseMessage response = await PostAsync(client, corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should()
            .Contain("uniplus.publicacoes.tipo_ato.resultado_com_efeito_irreversivel");
    }

    [Fact(DisplayName = "POST com janela vazia retorna 422 — o fim é exclusivo")]
    public async Task Criar_ComJanelaVazia_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(
            client, new CorpoDoTipoDeAto(CodigoUnico()) { VigenciaFim = Inicio });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <remarks>
    /// A ADR-0027 exclui PUT puro do Idempotency-Key: repetir a mesma substituição é inócuo por construção.
    /// </remarks>
    [Fact(DisplayName = "PUT atualiza e retorna 204, sem exigir Idempotency-Key")]
    public async Task Atualizar_Retorna204SemIdempotencyKey()
    {
        string codigo = CodigoUnico();
        Guid id = await _apoio.CriarAsync(codigo);
        CorpoDoTipoDeAto corpo = new(codigo) { Id = id, Nome = "Nome atualizado" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await AtualizarAsync(client, id, corpo);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument doc = await LerAsync(client, new Uri($"{Base}/{id}", UriKind.Relative));
        doc.RootElement.GetProperty("nome").GetString().Should().Be("Nome atualizado");
    }

    [Fact(DisplayName = "PUT com Id divergente retorna 400 com o código da fronteira")]
    public async Task Atualizar_IdDivergente_Retorna400ComOCodigoDaFronteira()
    {
        string codigo = CodigoUnico();
        Guid id = await _apoio.CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await AtualizarAsync(
            client, Guid.NewGuid(), new CorpoDoTipoDeAto(codigo) { Id = id });

        await RecusaDoIdDivergente.ConferirAsync(response);
    }

    [Fact(DisplayName = "PUT de tipo inexistente retorna 404")]
    public async Task Atualizar_Inexistente_Retorna404()
    {
        Guid id = Guid.NewGuid();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await AtualizarAsync(
            client, id, new CorpoDoTipoDeAto(CodigoUnico()) { Id = id });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PUT com Id divergente é recusado sem alterar o tipo")]
    public async Task Atualizar_IdDivergente_NaoAlteraOTipo()
    {
        string codigo = CodigoUnico();
        Guid id = await _apoio.CriarAsync(codigo);
        CorpoDoTipoDeAto corpo = new(codigo) { Id = id, Nome = "Nome que a recusa não grava" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage recusa = await AtualizarAsync(client, Guid.NewGuid(), corpo);

        recusa.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument doc = await LerAsync(client, new Uri($"{Base}/{id}", UriKind.Relative));
        doc.RootElement.GetProperty("nome").GetString().Should().Be("Tipo de ato de teste");
    }

    [Fact(DisplayName = "PUT com Id vazio na rota e no corpo é recusado pela validação do comando, não como divergente")]
    public async Task Atualizar_IdVazioNaRotaENoCorpo_NaoEhDivergente()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        CorpoDoTipoDeAto corpo = new(CodigoUnico()) { Id = Guid.Empty };

        HttpResponseMessage response = await AtualizarAsync(client, Guid.Empty, corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("divergente");
    }

    [Fact(DisplayName = "DELETE remove, retorna 204 e o tipo some da leitura")]
    public async Task Remover_Retorna204ESomeDaLeitura()
    {
        string codigo = CodigoUnico();
        Guid id = await _apoio.CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"{Admin}/{id}", UriKind.Relative));
        Autenticar(request);

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync(new Uri($"{Base}/{id}", UriKind.Relative))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync(new Uri($"{Base}/{codigo}/vigente", UriKind.Relative))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DELETE de tipo inexistente retorna 404")]
    public async Task Remover_Inexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"{Admin}/{Guid.NewGuid()}", UriKind.Relative));
        Autenticar(request);

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
