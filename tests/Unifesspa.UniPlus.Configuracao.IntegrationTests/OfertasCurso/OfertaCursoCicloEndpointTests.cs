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
public sealed class OfertaCursoCicloEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosEndpointsDeOfertaCurso _apoio;

    public OfertaCursoCicloEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosEndpointsDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "Ciclo CRUD completo: POST → PUT (204, guard revalidado) → GET reflete → DELETE (204) → GET 404")]
    public async Task CicloCrudCompleto_CriaEditaRemove()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("isaude-oferta", "ISAUDE", "OFC004");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOferta(dependencias));

        await ConferirRecusaDaTransicaoParaParforSemBaseLegalAsync(client, id);
        await AtualizarParaParforADistanciaComBaseLegalAsync(client, id);
        await ConferirOfertaAtualizadaAsync(client, id, dependencias.CursoId);
        await ConferirRemocaoComoSoftDeleteAsync(client, id);
    }

    [Fact(DisplayName = "CA-05: curso com oferta viva bloqueia DELETE do curso (409); remover a oferta libera (204)")]
    public async Task Ca05_OfertaVivaBloqueiaRemocaoDoCurso()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("igeo-oferta", "IGEOF", "OFC005");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid ofertaId = await CriarOfertaAsync(client, CorpoOferta(dependencias));

        (await RemoverCursoAsync(client, dependencias.CursoId)).Should().Be(
            HttpStatusCode.Conflict, "ReferenciadoPorOfertaCursoVivaAsync agora consulta ofertas_de_curso de verdade (CA-05)");
        (await RemoverOfertaAsync(client, ofertaId)).Should().Be(HttpStatusCode.NoContent);
        (await RemoverCursoAsync(client, dependencias.CursoId)).Should().Be(
            HttpStatusCode.NoContent, "o soft-delete da oferta libera o curso para remoção");
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso?cursoId={id} retorna só as ofertas do curso; curso sem oferta → 200 vazio")]
    public async Task Listar_FiltraPorCursoId_RetornaSomenteDoCurso()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("facomp-755-a", "FCP755A", "OFC755A");
        Guid outroCurso = await _apoio.SemearCursoAsync("Arquitetura e Urbanismo");
        using HttpClient client = _fixture.Factory.CreateClient();
        await CriarOfertaAsync(client, CorpoOferta(dependencias));
        await CriarOfertaAsync(client, CorpoOferta(dependencias));
        await CriarOfertaAsync(client, CorpoOferta(dependencias with { CursoId = outroCurso }));

        await ConferirSomenteAsOfertasDoCursoAsync(client, dependencias.CursoId, quantidadeEsperada: 2);
        await ConferirCursoSemOfertasAsync(client);
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso preserva cursoId no header Link (self e next)")]
    public async Task Listar_ComFiltroCursoId_PreservaQueryParamNoLink()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("facomp-755-b", "FCP755B", "OFC755B");
        using HttpClient client = _fixture.Factory.CreateClient();
        // Duas ofertas do mesmo curso → com limit=1 há próxima página (rel=next).
        await CriarOfertaAsync(client, CorpoOferta(dependencias));
        await CriarOfertaAsync(client, CorpoOferta(dependencias));

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={dependencias.CursoId}&limit=1", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConferirFiltroNoHeaderLink(response, dependencias.CursoId);
    }

    // PUT Regular→Parfor sem base legal: guard revalidado na transição → 422.
    private static async Task ConferirRecusaDaTransicaoParaParforSemBaseLegalAsync(HttpClient client, Guid id)
    {
        Dictionary<string, object?> semBaseLegal = CorpoDeAtualizacao(id, NovoCodigoDaOferta());
        semBaseLegal["programaDeOferta"] = "PARFOR";

        using HttpResponseMessage transicaoInvalida = await EnviarPutAdmin(client, id, semBaseLegal);

        transicaoInvalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a transição Regular→Parfor sem base legal viola o guard condicional (ADR-0066)");
    }

    // PUT válido: Parfor com base legal, formato EAD — que também declara turno.
    private static async Task AtualizarParaParforADistanciaComBaseLegalAsync(HttpClient client, Guid id)
    {
        Dictionary<string, object?> valido = CorpoDeAtualizacao(id, NovoCodigoDaOferta());
        valido["programaDeOferta"] = "PARFOR";
        valido["formatoPedagogico"] = "EAD";
        valido["turnos"] = new[] { "NOTURNO" };
        valido["eMecCodigo"] = "654321";
        valido["vagasAnuaisAutorizadas"] = 0;
        valido["baseLegal"] = "Decreto 6.755/2009";

        using HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, valido);

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task ConferirOfertaAtualizadaAsync(HttpClient client, Guid id, Guid cursoId)
    {
        using JsonDocument doc = await LerOfertaAsync(client, id);
        JsonElement root = doc.RootElement;
        root.GetProperty("programaDeOferta").GetProperty("codigo").GetString().Should().Be("PARFOR");
        root.GetProperty("formatoPedagogico").GetProperty("codigo").GetString().Should().Be("EAD");
        root.GetProperty("regimeDeTurno").GetString().Should().Be("REGULAR");
        root.GetProperty("turnos").EnumerateArray().Select(t => t.GetString())
            .Should().Equal(["NOTURNO"], "a oferta a distância também declara turno");
        root.GetProperty("eMecCodigo").GetString().Should().Be("654321");
        root.GetProperty("vagasAnuaisAutorizadas").GetInt32().Should().Be(0, "zero é teto válido");
        root.GetProperty("baseLegal").GetString().Should().Be("Decreto 6.755/2009");
        root.GetProperty("cursoId").GetGuid().Should().Be(cursoId, "curso é imutável");
        root.GetProperty("unidadeOfertante").GetProperty("sigla").GetString()
            .Should().Be("ISAUDE", "o snapshot congelado é imutável pós-criação");
    }

    // DELETE: soft-delete simples — sem 409 (snapshots externos são desacoplados).
    private static async Task ConferirRemocaoComoSoftDeleteAsync(HttpClient client, Guid id)
    {
        (await RemoverOfertaAsync(client, id)).Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage aposRemocao = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));

        aposRemocao.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "o soft-delete tira a oferta das leituras via query filter global");
    }

    private static async Task<HttpStatusCode> RemoverOfertaAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage resposta = await EnviarDeleteAdmin(client, id);
        return resposta.StatusCode;
    }

    private static async Task<HttpStatusCode> RemoverCursoAsync(HttpClient client, Guid cursoId)
    {
        using HttpRequestMessage pedido = NovoDeleteAdmin($"/api/configuracao/admin/cursos/{cursoId}");
        using HttpResponseMessage resposta = await client.SendAsync(pedido);
        return resposta.StatusCode;
    }

    // Filtro por curso (limit folgado): só as ofertas do curso pedido.
    private static async Task ConferirSomenteAsOfertasDoCursoAsync(HttpClient client, Guid cursoId, int quantidadeEsperada)
    {
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={cursoId}&limit=50", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.ValueKind.Should().Be(JsonValueKind.Array, "a listagem devolve array JSON puro (ADR-0025)");
        root.GetArrayLength().Should().Be(quantidadeEsperada, "só as ofertas vivas do curso pedido");
        root.EnumerateArray().Select(item => item.GetProperty("cursoId").GetGuid()).Should().OnlyContain(id => id == cursoId);
    }

    // Curso sem ofertas → 200 com coleção vazia (sem 404).
    private static async Task ConferirCursoSemOfertasAsync(HttpClient client)
    {
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={Guid.NewGuid()}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    private static void ConferirFiltroNoHeaderLink(HttpResponseMessage response, Guid cursoId)
    {
        response.Headers.TryGetValues("Link", out IEnumerable<string>? links).Should().BeTrue();
        string link = links!.Single();
        link.Should().Contain("rel=\"next\"", "limit=1 com 2 ofertas filtradas deve emitir próxima página");
        link.Should().Contain($"cursoId={cursoId}", "o filtro de curso deve viajar em self e next para o cliente reanexar");
    }
}
