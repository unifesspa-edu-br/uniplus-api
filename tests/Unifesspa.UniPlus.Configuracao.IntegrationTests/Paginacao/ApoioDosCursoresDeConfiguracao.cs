namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using System.Net.Http.Headers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

/// <summary>
/// A prova de cursor sobre uma listagem do módulo Configuração, consultada por uma pessoa autenticada com o papel de
/// administração da plataforma: o cliente leva a identificação em todo pedido, inclusive nos links de continuação.
/// </summary>
internal sealed class ApoioDosCursoresDeConfiguracao
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    internal ApoioDosCursoresDeConfiguracao(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    internal HttpClient Cliente()
    {
        HttpClient cliente = _fixture.Factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "plataforma-admin");

        return cliente;
    }

    internal ProvaDoCursor Prova(string rota, string propriedadeDoId = "id") =>
        new(new ConsultaPaginada(Cliente(), rota, propriedadeDoId), new CursoresDeTeste(_fixture.Factory.Services));

    /// <summary>Remove os tipos de processo que o teste criou, para a lista pública dos demais testes não crescer.</summary>
    internal async Task RemoverTiposDeProcessoAsync(IReadOnlyCollection<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.TiposProcesso.IgnoreQueryFilters().Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync();
    }

    /// <summary>Remove os modelos de formulário que o teste criou.</summary>
    internal async Task RemoverModelosAsync(IReadOnlyCollection<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        Guid[] modelos = [.. ids];
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM configuracao.modelos_formulario WHERE id = ANY({modelos})");
    }

    /// <summary>Remove os termos de consentimento que o teste criou, com as versões deles, se houver.</summary>
    internal async Task RemoverTermosAsync(IReadOnlyCollection<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        Guid[] termos = [.. ids];
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM configuracao.versoes_do_termo_consentimento WHERE termo_consentimento_id = ANY({termos})");
        await db.Database.ExecuteSqlAsync($"DELETE FROM configuracao.termos_de_consentimento WHERE id = ANY({termos})");
    }

    /// <summary>Remove os cursos informados, as ofertas deles e os locais dessas ofertas.</summary>
    internal async Task RemoverCursosAsync(IReadOnlyCollection<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        Guid[] cursos = [.. ids];
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"""
            WITH ofertas_apagadas AS (
                DELETE FROM configuracao.ofertas_de_curso WHERE curso_id = ANY({cursos}) RETURNING local_oferta_id)
            DELETE FROM configuracao.locais_de_oferta WHERE id IN (SELECT local_oferta_id FROM ofertas_apagadas)
            """);
        await db.Database.ExecuteSqlAsync($"DELETE FROM configuracao.cursos WHERE id = ANY({cursos})");
    }

    /// <summary>Remove os cursos cujo nome começa pelo bloco, as ofertas deles e os locais dessas ofertas.</summary>
    internal async Task RemoverCursosDoBlocoAsync(string bloco)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bloco);

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"""
            WITH ofertas_apagadas AS (
                DELETE FROM configuracao.ofertas_de_curso
                WHERE curso_id IN (SELECT id FROM configuracao.cursos WHERE starts_with(nome, {bloco}))
                RETURNING local_oferta_id)
            DELETE FROM configuracao.locais_de_oferta WHERE id IN (SELECT local_oferta_id FROM ofertas_apagadas)
            """);
        await db.Database.ExecuteSqlAsync($"DELETE FROM configuracao.cursos WHERE starts_with(nome, {bloco})");
    }
}
