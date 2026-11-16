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
public sealed class OfertaCursoRegimesEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosEndpointsDeOfertaCurso _apoio;

    public OfertaCursoRegimesEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosEndpointsDeOfertaCurso(fixture);
    }

    [Theory(DisplayName = "POST com regime de turno e turnos incoerentes retorna 422 pelo wire code correspondente")]
    [InlineData(null, new[] { "MATUTINO" }, "uniplus.configuracao.oferta_curso.regime_de_turno_obrigatorio")]
    [InlineData("PARCIAL", new[] { "MATUTINO" }, "uniplus.configuracao.oferta_curso.regime_de_turno_invalido")]
    [InlineData("REGULAR", new string[0], "uniplus.configuracao.oferta_curso.turnos_obrigatorios")]
    [InlineData("REGULAR", new[] { "INTEGRAL" }, "uniplus.configuracao.oferta_curso.turno_invalido")]
    [InlineData("REGULAR", new[] { "DIURNO" }, "uniplus.configuracao.oferta_curso.turno_invalido")]
    [InlineData("INTEGRAL", new[] { "MATUTINO", "MATUTINO" }, "uniplus.configuracao.oferta_curso.turno_repetido")]
    [InlineData("REGULAR", new[] { "MATUTINO", "NOTURNO" }, "uniplus.configuracao.oferta_curso.cardinalidade_turnos_incompativel_com_regime")]
    [InlineData("INTEGRAL", new[] { "NOTURNO" }, "uniplus.configuracao.oferta_curso.cardinalidade_turnos_incompativel_com_regime")]
    public async Task Criar_RegimeOuTurnosInvalidos_Retorna422ComWireCode(
        string? regimeDeTurno, string[] turnos, string wireCodeEsperado)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("regime"), NovaSigla(), NovoCodigo());

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno,
            turnos,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith(wireCodeEsperado,
                "cada violação de regime ou turno tem wire code próprio no catálogo público");
    }

    [Theory(DisplayName = "POST com regime de funcionamento ausente, nulo ou fora do domínio retorna 422 pelo wire code correspondente")]
    [InlineData(null, "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio")]
    [InlineData("", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio")]
    [InlineData("   ", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio")]
    [InlineData("SEMI_INTENSIVO", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_invalido")]
    [InlineData("Intensivo", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_invalido")]
    [InlineData("INTEGRAL", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_invalido")]
    public async Task Criar_RegimeDeFuncionamentoInvalido_Retorna422ComWireCode(
        string? regimeDeFuncionamento, string wireCodeEsperado)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("func"), NovaSigla(), NovoCodigo());

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento,
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith(wireCodeEsperado,
                "ausência e token fora do domínio têm wire codes distintos no catálogo público");
    }

    [Fact(DisplayName = "POST omitindo o campo regimeDeFuncionamento retorna 422 de obrigatório — o campo ausente chega à validação de domínio")]
    public async Task Criar_SemCampoRegimeDeFuncionamento_Retorna422()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("omisso"), NovaSigla(), NovoCodigo());

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeTurno = "INTEGRAL",
            turnos = new[] { "MATUTINO", "VESPERTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith("uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio",
                "dois turnos sob INTEGRAL não presumem oferta intensiva");
    }

    [Fact(DisplayName = "POST de oferta intensiva com regime de turno regular retorna 422 de incompatibilidade e não persiste")]
    public async Task Criar_IntensivoComRegular_Retorna422ENaoPersiste()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("intensivo"), NovaSigla(), NovoCodigo());

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "INTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith(
                "uniplus.configuracao.oferta_curso.regime_de_funcionamento_incompativel_com_regime_de_turno");

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext =
            escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<OfertaCurso> persistidas = await dbContext.OfertasCurso
            .Where(o => o.CursoId == cursoId && o.LocalOfertaId == localId)
            .ToListAsync(CancellationToken.None);
        persistidas.Should().BeEmpty("a recusa de domínio precede qualquer escrita");
    }

    [Fact(DisplayName = "POST de oferta intensiva integral é criada e a leitura devolve as duas dimensões declaradas")]
    public async Task Criar_IntensivoIntegral_CriaEDevolveAsDuasDimensoes()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("intint"), NovaSigla(), NovoCodigo());

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "INTENSIVO",
            regimeDeTurno = "INTEGRAL",
            turnos = new[] { "MATUTINO", "VESPERTINO" },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("regimeDeFuncionamento").GetString().Should().Be("INTENSIVO");
        root.GetProperty("regimeDeTurno").GetString().Should().Be("INTEGRAL");
        root.GetProperty("turnos").EnumerateArray().Select(t => t.GetString())
            .Should().Equal(["MATUTINO", "VESPERTINO"]);
    }

    [Fact(DisplayName = "POST em alternância pedagógica é criada, o PUT troca o regime e a leitura devolve o valor declarado")]
    public async Task CicloAlternanciaPedagogica_CriaAtualizaELe()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("alternancia"), NovaSigla(), NovoCodigo());

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "ALTERNANCIA_PEDAGOGICA",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage aposCriacao = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        aposCriacao.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await aposCriacao.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("regimeDeFuncionamento").GetString()
                .Should().Be("ALTERNANCIA_PEDAGOGICA");
            doc.RootElement.GetProperty("regimeDeTurno").GetString().Should().Be("REGULAR");
        }

        HttpResponseMessage criarParaAtualizacao = await EnviarPostAdmin(client, new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        });
        criarParaAtualizacao.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid idParaAtualizacao = await criarParaAtualizacao.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage atualizar = await EnviarPutAdmin(client, idParaAtualizacao, new
        {
            id = idParaAtualizacao,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "ALTERNANCIA_PEDAGOGICA",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "VESPERTINO" },
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage aposAtualizacao = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{idParaAtualizacao}", UriKind.Relative));
        aposAtualizacao.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument docAtualizado =
            JsonDocument.Parse(await aposAtualizacao.Content.ReadAsStringAsync());
        docAtualizado.RootElement.GetProperty("regimeDeFuncionamento").GetString()
            .Should().Be("ALTERNANCIA_PEDAGOGICA");
        docAtualizado.RootElement.GetProperty("regimeDeTurno").GetString().Should().Be("REGULAR");
    }

    [Fact(DisplayName = "PUT que torna a oferta intensiva sem trocar o regime de turno retorna 422 e não converte os valores")]
    public async Task Atualizar_ParaIntensivoMantendoRegular_Retorna422SemConverter()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("putint"), NovaSigla(), NovoCodigo());

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, new
        {
            id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "INTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = new[] { "MATUTINO" },
        });

        atualizar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument erro = JsonDocument.Parse(await atualizar.Content.ReadAsStringAsync());
        erro.RootElement.GetProperty("type").GetString()
            .Should().EndWith(
                "uniplus.configuracao.oferta_curso.regime_de_funcionamento_incompativel_com_regime_de_turno");

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("regimeDeFuncionamento").GetString()
            .Should().Be("EXTENSIVO", "a recusa preserva o valor declarado, sem conversão silenciosa");
        doc.RootElement.GetProperty("regimeDeTurno").GetString().Should().Be("REGULAR");
    }

    [Theory(DisplayName = "POST sem turno é recusado em qualquer formato pedagógico — a distância inclusive")]
    [InlineData("EAD")]
    [InlineData("SEMIPRESENCIAL")]
    [InlineData("PRESENCIAL")]
    public async Task Criar_SemTurnoEmQualquerFormato_Retorna422(string formatoPedagogico)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("formato"), NovaSigla(), NovoCodigo());

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            formatoPedagogico,
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "REGULAR",
            turnos = Array.Empty<string>(),
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith("uniplus.configuracao.oferta_curso.turnos_obrigatorios",
                "nenhum formato pedagógico abre exceção ao turno");
    }
}
