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
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync("isaude-oferta", "ISAUDE", "OFC004");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        // PUT Regular→Parfor sem base legal: guard revalidado na transição → 422.
        var putSemBase = new
        {
            id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "PARFOR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };
        HttpResponseMessage transicaoInvalida = await EnviarPutAdmin(client, id, putSemBase);
        transicaoInvalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a transição Regular→Parfor sem base legal viola o guard condicional (ADR-0066)");

        // PUT válido: Parfor com base legal, formato EAD — que também declara turno.
        var putValido = new
        {
            id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "PARFOR",
            formatoPedagogico = "EAD",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "NOTURNO" },
            eMecCodigo = "654321",
            vagasAnuaisAutorizadas = 0,
            baseLegal = "Decreto 6.755/2009",
        };
        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, putValido);
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync()))
        {
            JsonElement root = doc.RootElement;
            root.GetProperty("programaDeOferta").GetString().Should().Be("PARFOR");
            root.GetProperty("formatoPedagogico").GetString().Should().Be("EAD");
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
        HttpResponseMessage remover = await EnviarDeleteAdmin(client, id);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage aposRemocao = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        aposRemocao.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "o soft-delete tira a oferta das leituras via query filter global");
    }

    [Fact(DisplayName = "CA-05: curso com oferta viva bloqueia DELETE do curso (409); remover a oferta libera (204)")]
    public async Task Ca05_OfertaVivaBloqueiaRemocaoDoCurso()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync("igeo-oferta", "IGEOF", "OFC005");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid ofertaId = await criar.Content.ReadFromJsonAsync<Guid>();

        // Curso referenciado por oferta viva → remoção bloqueada (409).
        using HttpRequestMessage removerCursoBloqueado = NovoDeleteAdmin(
            $"/api/configuracao/admin/cursos/{cursoId}");
        HttpResponseMessage bloqueado = await client.SendAsync(removerCursoBloqueado);
        bloqueado.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "ReferenciadoPorOfertaCursoVivaAsync agora consulta ofertas_de_curso de verdade (CA-05)");

        // Remover a oferta (204) libera o curso.
        HttpResponseMessage removerOferta = await EnviarDeleteAdmin(client, ofertaId);
        removerOferta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpRequestMessage removerCursoLiberado = NovoDeleteAdmin(
            $"/api/configuracao/admin/cursos/{cursoId}");
        HttpResponseMessage liberado = await client.SendAsync(removerCursoLiberado);
        liberado.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "o soft-delete da oferta libera o curso para remoção");
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso?cursoId={id} retorna só as ofertas do curso; curso sem oferta → 200 vazio")]
    public async Task Listar_FiltraPorCursoId_RetornaSomenteDoCurso()
    {
        (Guid cursoA, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Guid cursoB = await _apoio.SemearCursoAsync("Arquitetura e Urbanismo");
        Unidade unidade = await _apoio.SemearUnidadeAsync("facomp-755-a", "FCP755A", "OFC755A");

        using HttpClient client = _fixture.Factory.CreateClient();
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarPostAdmin(client, CorpoOferta(cursoB, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Filtro por A (limit folgado): exatamente as 2 ofertas de A, nenhuma de B.
        HttpResponseMessage respA = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={cursoA}&limit=50", UriKind.Relative));
        respA.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await respA.Content.ReadAsStringAsync()))
        {
            JsonElement root = doc.RootElement;
            root.ValueKind.Should().Be(JsonValueKind.Array, "a listagem devolve array JSON puro (ADR-0025)");
            root.GetArrayLength().Should().Be(2, "o curso A tem 2 ofertas vivas");
            foreach (JsonElement item in root.EnumerateArray())
            {
                item.GetProperty("cursoId").GetGuid().Should().Be(cursoA);
            }
        }

        // Curso sem ofertas → 200 com coleção vazia (sem 404).
        HttpResponseMessage respVazio = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={Guid.NewGuid()}", UriKind.Relative));
        respVazio.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await respVazio.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetArrayLength().Should().Be(0);
        }
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso preserva cursoId no header Link (self e next)")]
    public async Task Listar_ComFiltroCursoId_PreservaQueryParamNoLink()
    {
        (Guid cursoA, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync("facomp-755-b", "FCP755B", "OFC755B");

        using HttpClient client = _fixture.Factory.CreateClient();
        // Duas ofertas do mesmo curso → com limit=1 há próxima página (rel=next).
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={cursoA}&limit=1", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Link", out IEnumerable<string>? links).Should().BeTrue();
        string link = links!.Single();
        link.Should().Contain("rel=\"next\"", "limit=1 com 2 ofertas filtradas deve emitir próxima página");
        link.Should().Contain($"cursoId={cursoA}", "o filtro de curso deve viajar em self e next para o cliente reanexar");
    }
}
