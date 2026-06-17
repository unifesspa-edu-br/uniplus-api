namespace Unifesspa.UniPlus.Configuracao.IntegrationTests;

using System.Linq;

using AwesomeAssertions;

using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// CA-01 (#587): a <c>Cidade</c> é dado do módulo <c>Geo</c> (ADR-0090) — o
/// módulo Configuração não tem entidade/<c>DbSet</c>/tabela nem rota de cidade.
/// </summary>
public sealed class ConfiguracaoSemEntidadeCidadeTests : IClassFixture<ConfiguracaoApiFactory>
{
    private readonly ConfiguracaoApiFactory _factory;

    public ConfiguracaoSemEntidadeCidadeTests(ConfiguracaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "ConfiguracaoDbContext.Model não declara nenhuma entidade/tabela 'Cidade'")]
    public void Model_NaoTemEntidadeCidade()
    {
        using ConfiguracaoDbContext context = CriarContextoInMemory();

        IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IEntityType> tipos = context.Model.GetEntityTypes();

        tipos.Select(t => t.ClrType.Name)
            .Should().NotContain(name => name.Contains("Cidade", StringComparison.OrdinalIgnoreCase),
                "a Cidade vive no módulo Geo — Configuração só guarda a referência por código (ADR-0090)");

        tipos.Select(t => t.GetTableName())
            .Should().NotContain(tabela => tabela != null && tabela.Contains("cidade", StringComparison.OrdinalIgnoreCase)
                && !tabela.Contains("codigo_ibge", StringComparison.OrdinalIgnoreCase),
                "não há tabela 'cidade' em uniplus_configuracao");
    }

    [Fact(DisplayName = "Não existe rota /api/.../cidades no módulo Configuração")]
    public void Rotas_NaoExpoemCidades()
    {
        // CreateClient força a materialização do host (e do roteamento).
        using HttpClient _ = _factory.CreateClient();

        IEnumerable<EndpointDataSource> sources = _factory.Services.GetServices<EndpointDataSource>();

        IEnumerable<string> rawTexts = sources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty);

        rawTexts.Should().NotContain(
            pattern => pattern.Contains("cidades", StringComparison.OrdinalIgnoreCase),
            "Cidade não é cadastro de Configuração (ADR-0090)");
    }

    private static ConfiguracaoDbContext CriarContextoInMemory()
    {
        DbContextOptions<ConfiguracaoDbContext> options = new DbContextOptionsBuilder<ConfiguracaoDbContext>()
            .UseInMemoryDatabase(nameof(ConfiguracaoSemEntidadeCidadeTests))
            .Options;

        return new ConfiguracaoDbContext(options);
    }
}
