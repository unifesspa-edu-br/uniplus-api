namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A corrida entre a conferência de unicidade e a gravação da Unidade responde 409, e não 500, e o reenvio com a
/// mesma Idempotency-Key é executado de novo e devolve o mesmo 409, sem reproduzir resposta guardada. A corrida é
/// simulada com o repositório de conferência obsoleta no host.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeCorridaEndpointTests
{
    private const string CodigoDoSlugEmUso = "uniplus.organizacao.unidade.slug_ja_existe";

    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeCorridaEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST que colide no slug na corrida responde 409 e o reenvio com a mesma chave devolve o mesmo 409")]
    public async Task Criar_CorridaNoSlug_Responde409ENoReenvioOMesmo409()
    {
        Unidade existente = await SemearUnidadeAsync();

        await ConferirQueOConflitoDoSlugSeRepeteNoReenvioAsync(HttpMethod.Post, "unidades", ComSlugDe(existente));
    }

    [Fact(DisplayName = "PUT que passa a usar o slug de outra Unidade na corrida responde 409 e o reenvio com a mesma chave devolve o mesmo 409")]
    public async Task Atualizar_CorridaNoSlug_Responde409ENoReenvioOMesmo409()
    {
        Unidade a = await SemearUnidadeAsync();
        Unidade b = await SemearUnidadeAsync();

        await ConferirQueOConflitoDoSlugSeRepeteNoReenvioAsync(
            HttpMethod.Put, $"unidades/{b.Id}", CorpoDaAtualizacao(b, slug: a.Slug.Valor));
    }

    private async Task ConferirQueOConflitoDoSlugSeRepeteNoReenvioAsync(HttpMethod metodo, string recurso, object corpo)
    {
        var consultas = new ConsultasAoCadastroContadas();
        await using WebApplicationFactory<HostAssemblyMarker> host = ComConsultaObsoleta(consultas);
        using HttpClient client = host.CreateClient();
        string chave = Guid.NewGuid().ToString();

        RespostaDoConflito primeiro = await EnviarAsync(client, metodo, recurso, corpo, chave);
        int consultasDaPrimeira = consultas.Total;
        RespostaDoConflito reenvio = await EnviarAsync(client, metodo, recurso, corpo, chave);

        primeiro.Should().Be(new RespostaDoConflito(HttpStatusCode.Conflict, CodigoDoSlugEmUso, Reproduzida: false));
        reenvio.Should().Be(new RespostaDoConflito(HttpStatusCode.Conflict, CodigoDoSlugEmUso, Reproduzida: false));
        consultasDaPrimeira.Should().BeGreaterThan(0, "a conferência obsoleta precisa ter sido a consultada pelo handler");
        consultas.Total.Should().BeGreaterThan(consultasDaPrimeira, "o reenvio é executado de novo, não reproduzido");
    }

    private static object ComSlugDe(Unidade existente)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        return new
        {
            nome = "Unidade da corrida",
            slug = existente.Slug.Valor,
            sigla = sufixo,
            codigo = sufixo,
            tipo = TipoUnidade.Centro.Codigo(),
            unidadeAcademica = false,
            vigenciaInicio = "2026-01-01",
        };
    }

    private static object CorpoDaAtualizacao(Unidade unidade, string slug) =>
        new
        {
            id = unidade.Id,
            nome = unidade.Nome,
            slug,
            sigla = unidade.Sigla,
            codigo = unidade.Codigo,
            tipo = TipoUnidade.Centro.Codigo(),
            unidadeAcademica = false,
        };

    private WebApplicationFactory<HostAssemblyMarker> ComConsultaObsoleta(ConsultasAoCadastroContadas consultas) =>
        _fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            // Registro por tipo, como o do repositório real: o codegen do Wolverine monta o handler sem service
            // location (ADR-0098), o que uma fábrica lambda impediria.
            services.RemoveAll<IUnidadeRepository>();
            services.AddScoped<UnidadeRepository>();
            services.AddSingleton(consultas);
            services.AddScoped<IUnidadeRepository, UnidadeComConsultaObsoleta>();
        }));

    private async Task<Unidade> SemearUnidadeAsync()
    {
        Unidade unidade = NovaUnidade();

        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        UniPlusDbContext ctx = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ctx.Unidades.Add(unidade);
        await ctx.SaveChangesAsync();
        return unidade;
    }

    private static Unidade NovaUnidade()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        return Unidade.Criar(
            "Unidade existente",
            new IdentificadoresDaUnidade($"u-{sufixo}", sufixo, sufixo, null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(new DateOnly(2026, 1, 1), null),
            null,
            TipoUnidade.Centro,
            false).Value!;
    }

    private static async Task<RespostaDoConflito> EnviarAsync(
        HttpClient client, HttpMethod metodo, string recurso, object corpo, string chave)
    {
        using HttpRequestMessage request = new(metodo, new Uri($"/api/organizacao/admin/{recurso}", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", chave);

        using HttpResponseMessage resposta = await client.SendAsync(request);
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        string codigo = documento.RootElement.TryGetProperty("code", out JsonElement valor) ? valor.GetString() ?? string.Empty : string.Empty;
        return new RespostaDoConflito(resposta.StatusCode, codigo, resposta.Headers.Contains("Idempotency-Replayed"));
    }

    private sealed record RespostaDoConflito(HttpStatusCode Status, string Codigo, bool Reproduzida);
}
