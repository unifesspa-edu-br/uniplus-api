namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDosEndpointsDeOfertaCurso;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OfertaCursoCriacaoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosEndpointsDeOfertaCurso _apoio;

    public OfertaCursoCriacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosEndpointsDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "POST cria (201) congelando o snapshot da Unidade REAL semeada; o GET expõe o sub-objeto + HATEOAS")]
    public async Task Criar_CongelaUnidadeRealSemeada()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync("faceel-oferta", "FACEEL", "OFC001");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            formatoPedagogico = "PRESENCIAL",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "INTEGRAL",
            turnos = new[] { "VESPERTINO", "MATUTINO" },
            eMecCodigo = "123456",
            codigoSga = "ENG-01",
            vagasAnuaisAutorizadas = 40,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("cursoId").GetGuid().Should().Be(cursoId);
        root.GetProperty("localOfertaId").GetGuid().Should().Be(localId);
        root.GetProperty("programaDeOferta").GetString().Should().Be("REGULAR");
        root.GetProperty("formatoPedagogico").GetString().Should().Be("PRESENCIAL");
        root.GetProperty("regimeDeFuncionamento").GetString().Should().Be("EXTENSIVO");
        root.GetProperty("regimeDeTurno").GetString().Should().Be("INTEGRAL");
        root.GetProperty("turnos").EnumerateArray().Select(t => t.GetString())
            .Should().Equal(["MATUTINO", "VESPERTINO"],
                "a leitura devolve os turnos em ordem canônica, não na ordem enviada");
        root.GetProperty("eMecCodigo").GetString().Should().Be("123456");
        root.GetProperty("codigoSga").GetString().Should().Be("ENG-01");
        root.GetProperty("vagasAnuaisAutorizadas").GetInt32().Should().Be(40);

        // Congelamento (ADR-0061): o sub-objeto reflete a Unidade viva resolvida
        // in-process pelo IUnidadeReader no ato da criação.
        JsonElement unidadeOfertante = root.GetProperty("unidadeOfertante");
        unidadeOfertante.GetProperty("origemId").GetGuid().Should().Be(unidade.Id);
        unidadeOfertante.GetProperty("sigla").GetString().Should().Be("FACEEL");
        unidadeOfertante.GetProperty("nome").GetString().Should().Be("Unidade FACEEL");
        unidadeOfertante.GetProperty("tipo").GetString().Should().Be(nameof(TipoUnidade.Centro));

        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST com unidade ofertante inexistente retorna 422 — não há identidade viva para congelar")]
    public async Task Criar_UnidadeInexistente_Retorna422()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = Guid.NewGuid(),
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com curso inexistente retorna 422")]
    public async Task Criar_CursoInexistente_Retorna422()
    {
        (_, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync("ilinguas-oferta", "ILINGUAS", "OFC002");

        var body = new
        {
            cursoId = Guid.NewGuid(),
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com programa não-REGULAR sem base legal retorna 422 (guard de domínio)")]
    public async Task Criar_ProgramaNaoRegularSemBaseLegal_Retorna422()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync("iedar-oferta", "IEDAR", "OFC003");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "PARFOR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com código já usado por oferta viva devolve 409 codigo_ja_em_uso; o GET expõe o código")]
    public async Task Criar_CodigoJaEmUso_Devolve409()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("codigo-oferta-409"), NovaSigla(), NovoCodigo());
        string codigo = NovoCodigoDaOferta();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeira = await EnviarPostAdmin(client, CorpoOferta(cursoId, localId, unidade.Id, codigo));
        HttpResponseMessage repetida = await EnviarPostAdmin(client, CorpoOferta(cursoId, localId, unidade.Id, codigo));

        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repetida.Content.ReadAsStringAsync()).Should().Contain("uniplus.configuracao.oferta_curso.codigo_ja_em_uso");
        Guid id = await primeira.Content.ReadFromJsonAsync<Guid>();
        using JsonDocument doc = JsonDocument.Parse(await (await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative))).Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("codigo").GetString().Should().Be(codigo);
    }

    [Theory(DisplayName = "POST com código fora do formato devolve 422 em erro do campo codigo")]
    [InlineData("enf_maraba")]
    [InlineData("1ENF")]
    [InlineData("E")]
    [InlineData("ENF-MARABA")]
    public async Task Criar_CodigoForaDoFormato_Devolve422(string codigo)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("codigo-oferta-422"), NovaSigla(), NovoCodigo());

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, CorpoOferta(cursoId, localId, unidade.Id, codigo));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("codigo");
    }

    [Fact(DisplayName = "PUT troca o código mantendo o id; o próprio código não conflita e o de outra oferta devolve 409")]
    public async Task Atualizar_TrocaDeCodigo_RespeitaUnicidadeEntreVivas()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("codigo-oferta-put"), NovaSigla(), NovoCodigo());
        string codigoA = NovoCodigoDaOferta();
        string codigoB = NovoCodigoDaOferta();

        using HttpClient client = _fixture.Factory.CreateClient();
        Guid idA = await (await EnviarPostAdmin(client, CorpoOferta(cursoId, localId, unidade.Id, codigoA))).Content.ReadFromJsonAsync<Guid>();
        await EnviarPostAdmin(client, CorpoOferta(cursoId, localId, unidade.Id, codigoB));

        HttpResponseMessage mesmoCodigo = await EnviarPutAdmin(client, idA, CorpoDeAtualizacao(idA, codigoA));
        HttpResponseMessage codigoDeOutra = await EnviarPutAdmin(client, idA, CorpoDeAtualizacao(idA, codigoB));
        HttpResponseMessage codigoNovo = await EnviarPutAdmin(client, idA, CorpoDeAtualizacao(idA, NovoCodigoDaOferta()));

        mesmoCodigo.StatusCode.Should().Be(HttpStatusCode.NoContent);
        codigoDeOutra.StatusCode.Should().Be(HttpStatusCode.Conflict);
        codigoNovo.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A busca q da listagem casa também com o código da oferta")]
    public async Task Listar_ComBuscaPeloCodigo_EncontraAOferta()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("codigo-oferta-q"), NovaSigla(), NovoCodigo());
        string codigo = $"BUSCA_{Guid.CreateVersion7():N}".ToUpperInvariant();

        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await (await EnviarPostAdmin(client, CorpoOferta(cursoId, localId, unidade.Id, codigo))).Content.ReadFromJsonAsync<Guid>();
        await EnviarPostAdmin(client, CorpoOferta(cursoId, localId, unidade.Id));

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?q={Uri.EscapeDataString(codigo)}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).Should().Equal(id);
    }
}
