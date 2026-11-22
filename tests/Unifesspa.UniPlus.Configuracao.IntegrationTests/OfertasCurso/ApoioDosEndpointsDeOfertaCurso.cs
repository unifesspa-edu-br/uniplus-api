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
using Unifesspa.UniPlus.Testes.Compartilhado;

internal sealed class ApoioDosEndpointsDeOfertaCurso
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    internal ApoioDosEndpointsDeOfertaCurso(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    internal static readonly DateOnly VigenciaInicio = new(2026, 1, 1);

    internal static readonly DateTimeOffset Agora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>O curso, o local e a unidade que uma oferta cita, já gravados.</summary>
    internal sealed record DependenciasDaOferta(Guid CursoId, Guid LocalOfertaId, Guid UnidadeId);

    internal static Dictionary<string, object?> CorpoDeAtualizacao(Guid id, string codigo) => new()
    {
        ["id"] = id,
        ["codigo"] = codigo,
        ["programaDeOferta"] = "REGULAR",
        ["regimeDeFuncionamento"] = "EXTENSIVO",
        ["regimeDeTurno"] = "REGULAR",
        ["turnos"] = new[] { "MATUTINO" },
        ["formatoPedagogico"] = "PRESENCIAL",
    };

    internal static Dictionary<string, object?> CorpoOferta(DependenciasDaOferta dependencias, string? codigo = null) => new()
    {
        ["cursoId"] = dependencias.CursoId,
        ["localOfertaId"] = dependencias.LocalOfertaId,
        ["unidadeOfertanteOrigemId"] = dependencias.UnidadeId,
        ["codigo"] = codigo ?? NovoCodigoDaOferta(),
        ["programaDeOferta"] = "REGULAR",
        ["regimeDeFuncionamento"] = "EXTENSIVO",
        ["regimeDeTurno"] = "REGULAR",
        ["turnos"] = new[] { "MATUTINO" },
        ["formatoPedagogico"] = "PRESENCIAL",
    };

    /// <summary>O corpo padrão da oferta, com os campos que o cenário troca.</summary>
    internal static Dictionary<string, object?> CorpoOfertaCom(
        DependenciasDaOferta dependencias, params (string Campo, object? Valor)[] ajustes)
    {
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        foreach ((string campo, object? valor) in ajustes)
        {
            corpo[campo] = valor;
        }

        return corpo;
    }

    internal static async Task<string?> CodigoDaOfertaAsync(HttpClient client, Guid id)
    {
        using JsonDocument doc = await LerOfertaAsync(client, id);
        return doc.RootElement.GetProperty("codigo").GetString();
    }

    /// <summary>Semeia curso, local e uma unidade de identificadores sorteados, únicos entre as unidades vivas.</summary>
    internal async Task<DependenciasDaOferta> SemearDependenciasAsync(string prefixoDoSlug)
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync("Engenharia Civil");
        Unidade unidade = await SemearUnidadeAsync(NovoSlug(prefixoDoSlug), NovaSigla(), NovoCodigo());
        return new DependenciasDaOferta(cursoId, localId, unidade.Id);
    }

    /// <summary>Semeia curso, local e uma unidade de identificadores conhecidos pelo teste.</summary>
    internal async Task<DependenciasDaOferta> SemearDependenciasAsync(string slug, string sigla, string codigo)
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync("Engenharia Civil");
        Unidade unidade = await SemearUnidadeAsync(slug, sigla, codigo);
        return new DependenciasDaOferta(cursoId, localId, unidade.Id);
    }

    internal static async Task<Guid> CriarOfertaAsync(HttpClient client, object body)
    {
        using HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    internal static async Task<JsonDocument> LerOfertaAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
    }

    internal async Task ConferirRecusaNaCriacaoAsync(object body, string wireCodeEsperado, string porque = "")
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await EnviarPostAdmin(client, body);
        await ConferirRecusa422Async(resposta, wireCodeEsperado, porque);
    }

    internal static async Task ConferirRecusa422Async(HttpResponseMessage resposta, string wireCodeEsperado, string porque = "")
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString().Should().EndWith(wireCodeEsperado, porque);
    }

    internal async Task<IReadOnlyList<OfertaCurso>> OfertasGravadasAsync(Guid cursoId, Guid localId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await dbContext.OfertasCurso
            .Where(o => o.CursoId == cursoId && o.LocalOfertaId == localId)
            .ToListAsync(CancellationToken.None);
    }

    internal async Task<Guid> SemearCursoAsync(string nome)
    {
        Curso curso = Curso.Criar(CodigoUnico(), nome, "Bacharelado", "Graduação", null).Value!;

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        dbContext.Cursos.Add(curso);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return curso.Id;
    }

    internal async Task<(Guid CursoId, Guid LocalOfertaId)> SemearCursoELocalAsync(string nomeCurso)
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), nomeCurso, "Bacharelado", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            new("CAMPUS_SEDE", null, "1504208", "Marabá", "PA", null, null),
            Agora).ValorEsperado();

        await PersistirAsync(curso, local);

        return (curso.Id, local.Id);
    }

    // Semeia pelo contexto do container do host — o schema `organizacao` foi criado pelas migrations
    // no boot; o IUnidadeReader (in-process, ADR-0056) enxerga a Unidade na criação da oferta.
    private async Task PersistirAsync(params object[] entidades)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        dbContext.AddRange(entidades);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    // Cada caso de um Theory semeia a própria unidade, e sigla e código são
    // únicos entre unidades vivas — daí os identificadores sorteados.
    internal static string NovoSlug(string prefixo) => $"{prefixo}-{Sufixo()}";

    internal static string NovaSigla() => $"U{Sufixo()}".ToUpperInvariant();

    internal static string NovoCodigo() => $"OFC{Sufixo()}".ToUpperInvariant();

    // O slug exige minúsculas (formato validado no value object da Unidade).
    internal static string Sufixo() => Guid.NewGuid().ToString("N")[..8];

    internal async Task<Unidade> SemearUnidadeAsync(string slug, string sigla, string codigo)
    {
        Unidade unidade = Unidade.Criar(
            $"Unidade {sigla}",
            new IdentificadoresDaUnidade(slug, sigla, codigo, null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(VigenciaInicio, null),
            null,
            TipoUnidade.Centro,
            true).Value!;

        await PersistirAsync(unidade);

        return unidade;
    }

    internal static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    internal static async Task<string> TotalDeOfertasDoCursoAsync(HttpClient client, Guid cursoId)
    {
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri(
                $"/api/configuracao/ofertas-curso?cursoId={cursoId}&include_total=true",
                UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return resposta.Headers.GetValues("x-total-count").Single();
    }

    // Semeia direto no contexto: os casos de total precisam de volume acima do teto de
    // página, e criar essa quantidade pelo endpoint mediria o custo do pipeline HTTP em
    // vez da contagem. O snapshot da unidade ofertante é congelado na criação e nenhum
    // destes casos o inspeciona — só a origem precisa ser a unidade real semeada.
    internal async Task SemearOfertasPorCursoAsync(IEnumerable<Guid> cursos, Guid localId, Guid unidadeOrigemId, int quantidadePorCurso)
    {
        object[] ofertas =
        [
            .. cursos.SelectMany(cursoId => Enumerable.Range(0, quantidadePorCurso).Select(_ => NovaOferta(cursoId, localId, unidadeOrigemId))),
        ];

        await PersistirAsync(ofertas);
    }

    private static OfertaCurso NovaOferta(Guid cursoId, Guid localId, Guid unidadeOrigemId)
    {
        UnidadeOfertante snapshot = UnidadeOfertante.Criar(
            unidadeOrigemId, "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

        return OfertaCurso.Criar(
            cursoId, localId, snapshot,
            new DeclaracaoDaOfertaDeCurso(NovoCodigoDaOferta(), "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"])).Value!;
    }

    internal static Dictionary<string, object?> CorpoDeAtualizacaoCom(Guid id, params (string Campo, object? Valor)[] ajustes)
    {
        Dictionary<string, object?> corpo = CorpoDeAtualizacao(id, NovoCodigoDaOferta());
        foreach ((string campo, object? valor) in ajustes)
        {
            corpo[campo] = valor;
        }

        return corpo;
    }

    internal static IReadOnlyList<string?> CamposComErro(string corpo)
    {
        using JsonDocument problema = JsonDocument.Parse(corpo);

        return [.. problema.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString())];
    }

    internal static async Task ConferirRecusaAsync(HttpResponseMessage resposta, string codigoPublicado, string campo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("code").GetString().Should().Be(codigoPublicado);
        CamposComErro(corpo).Should().Contain(campo);
    }

    internal static string NovoCodigoDaOferta() => $"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant();

    internal static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/ofertas-curso", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/ofertas-curso/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = NovoDeleteAdmin($"/api/configuracao/admin/ofertas-curso/{id}");
        return await client.SendAsync(request);
    }

    internal static HttpRequestMessage NovoDeleteAdmin(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }
}
