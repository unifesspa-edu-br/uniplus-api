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

internal sealed class ApoioDosEndpointsDeOfertaCurso
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    internal ApoioDosEndpointsDeOfertaCurso(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    internal static readonly DateOnly VigenciaInicio = new(2026, 1, 1);

    internal static readonly DateTimeOffset Agora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    internal static object CorpoDeAtualizacao(Guid id, string codigo) => new
    {
        id,
        codigo,
        programaDeOferta = "REGULAR",
        regimeDeFuncionamento = "EXTENSIVO",
        regimeDeTurno = "REGULAR",
        turnos = new[] { "MATUTINO" },
        formatoPedagogico = "PRESENCIAL",
    };

    internal static object CorpoOferta(Guid cursoId, Guid localOfertaId, Guid unidadeOfertanteOrigemId, string? codigo = null) => new
    {
        cursoId,
        localOfertaId,
        unidadeOfertanteOrigemId,
        codigo = codigo ?? NovoCodigoDaOferta(),
        programaDeOferta = "REGULAR",
        regimeDeFuncionamento = "EXTENSIVO",
        regimeDeTurno = "REGULAR",
        turnos = new[] { "MATUTINO" },
        formatoPedagogico = "PRESENCIAL",
    };

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

    internal async Task<(Guid CursoId, Guid LocalOfertaId)> SemearCursoELocalAsync()
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), "Engenharia Civil", "Bacharelado", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        dbContext.Cursos.Add(curso);
        dbContext.LocaisOferta.Add(local);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (curso.Id, local.Id);
    }

    internal async Task<(Guid CursoId, Guid LocalOfertaId)> SemearCursoELocalParaBuscaAsync(
        string nomeCurso)
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), nomeCurso, "Bacharelado", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        dbContext.Cursos.Add(curso);
        dbContext.LocaisOferta.Add(local);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (curso.Id, local.Id);
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

        // Semeia pelo contexto do container do host — o schema `organizacao` foi
        // criado pelas migrations no boot; o
        // IUnidadeReader (in-process, ADR-0056) enxerga a Unidade na criação da oferta.
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync(CancellationToken.None);

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
    internal async Task SemearOfertasAsync(Guid cursoId, Guid localId, Guid unidadeOrigemId, int quantidade)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        for (int i = 0; i < quantidade; i++)
        {
            UnidadeOfertante snapshot = UnidadeOfertante.Criar(
                unidadeOrigemId, "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;
            OfertaCurso oferta = OfertaCurso.Criar(
                cursoId, localId, snapshot,
                new DeclaracaoDaOfertaDeCurso($"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"])).Value!;
            dbContext.OfertasCurso.Add(oferta);
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
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
