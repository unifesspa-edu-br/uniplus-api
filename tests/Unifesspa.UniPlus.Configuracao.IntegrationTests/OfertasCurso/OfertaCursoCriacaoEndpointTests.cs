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
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("faceel-oferta", "FACEEL", "OFC001");
        object corpo = CorpoOfertaCom(
            dependencias, ("regimeDeTurno", "INTEGRAL"), ("turnos", new[] { "VESPERTINO", "MATUTINO" }),
            ("eMecCodigo", "123456"), ("codigoSga", "ENG-01"), ("vagasAnuaisAutorizadas", 40));

        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, corpo);

        using JsonDocument doc = await LerOfertaAsync(client, id);
        ConferirOfertaLida(doc.RootElement, dependencias);
        ConferirUnidadeCongelada(doc.RootElement.GetProperty("unidadeOfertante"), dependencias.UnidadeId);
        doc.RootElement.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST com unidade ofertante inexistente retorna 422 — não há identidade viva para congelar")]
    public async Task Criar_UnidadeInexistente_Retorna422()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync("Engenharia Civil");
        DependenciasDaOferta comUnidadeInexistente = new(cursoId, localId, Guid.NewGuid());

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await EnviarPostAdmin(client, CorpoOferta(comUnidadeInexistente));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com curso inexistente retorna 422")]
    public async Task Criar_CursoInexistente_Retorna422()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("ilinguas-oferta", "ILINGUAS", "OFC002");

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await EnviarPostAdmin(
            client, CorpoOferta(dependencias with { CursoId = Guid.NewGuid() }));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com programa não-REGULAR sem base legal retorna 422 (guard de domínio)")]
    public async Task Criar_ProgramaNaoRegularSemBaseLegal_Retorna422()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("iedar-oferta", "IEDAR", "OFC003");

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await EnviarPostAdmin(client, CorpoOfertaCom(dependencias, ("programaDeOferta", "PARFOR")));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com código já usado por oferta viva devolve 409 codigo_ja_em_uso; o GET expõe o código")]
    public async Task Criar_CodigoJaEmUso_Devolve409()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("codigo-oferta-409");
        string codigo = NovoCodigoDaOferta();

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage primeira = await EnviarPostAdmin(client, CorpoOferta(dependencias, codigo));
        using HttpResponseMessage repetida = await EnviarPostAdmin(client, CorpoOferta(dependencias, codigo));

        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repetida.Content.ReadAsStringAsync()).Should().Contain("uniplus.configuracao.oferta_curso.codigo_ja_em_uso");
        (await CodigoDaOfertaAsync(client, await primeira.Content.ReadFromJsonAsync<Guid>())).Should().Be(codigo);
    }

    [Theory(DisplayName = "POST com código fora do formato devolve 422 em erro do campo codigo")]
    [InlineData("enf_maraba")]
    [InlineData("1ENF")]
    [InlineData("E")]
    [InlineData("ENF-MARABA")]
    public async Task Criar_CodigoForaDoFormato_Devolve422(string codigo)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("codigo-oferta-422");

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await EnviarPostAdmin(client, CorpoOferta(dependencias, codigo));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("codigo");
    }

    [Fact(DisplayName = "PUT troca o código mantendo o id; o próprio código não conflita e o de outra oferta devolve 409")]
    public async Task Atualizar_TrocaDeCodigo_RespeitaUnicidadeEntreVivas()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("codigo-oferta-put");
        string codigoA = NovoCodigoDaOferta();
        string codigoB = NovoCodigoDaOferta();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid idA = await CriarOfertaAsync(client, CorpoOferta(dependencias, codigoA));
        await CriarOfertaAsync(client, CorpoOferta(dependencias, codigoB));

        HttpStatusCode[] resultados =
            [await AtualizarCodigoAsync(client, idA, codigoA), await AtualizarCodigoAsync(client, idA, codigoB), await AtualizarCodigoAsync(client, idA, NovoCodigoDaOferta())];

        resultados.Should().Equal(
            [HttpStatusCode.NoContent, HttpStatusCode.Conflict, HttpStatusCode.NoContent],
            "o próprio código não conflita, o de outra oferta devolve 409 e um código novo é aceito");
    }

    [Fact(DisplayName = "A busca q da listagem casa também com o código da oferta")]
    public async Task Listar_ComBuscaPeloCodigo_EncontraAOferta()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("codigo-oferta-q");
        string codigo = $"BUSCA_{Guid.CreateVersion7():N}".ToUpperInvariant();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOferta(dependencias, codigo));
        await CriarOfertaAsync(client, CorpoOferta(dependencias));

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/configuracao/ofertas-curso?q={Uri.EscapeDataString(codigo)}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).Should().Equal(id);
    }

    private static async Task<HttpStatusCode> AtualizarCodigoAsync(HttpClient client, Guid id, string codigo)
    {
        using HttpResponseMessage resposta = await EnviarPutAdmin(client, id, CorpoDeAtualizacao(id, codigo));
        return resposta.StatusCode;
    }

    private static void ConferirOfertaLida(JsonElement root, DependenciasDaOferta dependencias)
    {
        root.GetProperty("cursoId").GetGuid().Should().Be(dependencias.CursoId);
        root.GetProperty("localOfertaId").GetGuid().Should().Be(dependencias.LocalOfertaId);
        root.GetProperty("programaDeOferta").GetProperty("codigo").GetString().Should().Be("REGULAR");
        root.GetProperty("formatoPedagogico").GetProperty("codigo").GetString().Should().Be("PRESENCIAL");
        root.GetProperty("regimeDeFuncionamento").GetString().Should().Be("EXTENSIVO");
        root.GetProperty("regimeDeTurno").GetString().Should().Be("INTEGRAL");
        root.GetProperty("turnos").EnumerateArray().Select(t => t.GetString())
            .Should().Equal(["MATUTINO", "VESPERTINO"],
                "a leitura devolve os turnos em ordem canônica, não na ordem enviada");
        root.GetProperty("eMecCodigo").GetString().Should().Be("123456");
        root.GetProperty("codigoSga").GetString().Should().Be("ENG-01");
        root.GetProperty("vagasAnuaisAutorizadas").GetInt32().Should().Be(40);
    }

    // Congelamento (ADR-0061): o sub-objeto reflete a Unidade viva resolvida
    // in-process pelo IUnidadeReader no ato da criação.
    private static void ConferirUnidadeCongelada(JsonElement unidadeOfertante, Guid unidadeId)
    {
        unidadeOfertante.GetProperty("origemId").GetGuid().Should().Be(unidadeId);
        unidadeOfertante.GetProperty("sigla").GetString().Should().Be("FACEEL");
        unidadeOfertante.GetProperty("nome").GetString().Should().Be("Unidade FACEEL");
        unidadeOfertante.GetProperty("tipo").GetString().Should().Be(nameof(TipoUnidade.Centro));
    }
}
