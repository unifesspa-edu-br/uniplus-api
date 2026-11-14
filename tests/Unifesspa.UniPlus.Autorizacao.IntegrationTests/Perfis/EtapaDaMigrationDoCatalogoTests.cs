namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;

/// <summary>
/// A conferência do catálogo roda no mesmo lugar das migrations: no Job <c>ApplyAndExit</c> e na subida padrão, inclusive
/// sem migration pendente, e a falha dela é a falha da migration.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class EtapaDaMigrationDoCatalogoTests : PerfilEndpointTestBase, IDisposable
{
    private readonly List<IServiceScope> _escopos = [];

    public EtapaDaMigrationDoCatalogoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "No Job ApplyAndExit a etapa roda sem migration pendente e o Job termina com sucesso")]
    public async Task Job_ExecutaAEtapaSemMigrationPendente()
    {
        int execucoes = 0;
        using IHost host = Montar((_, _) =>
        {
            execucoes++;
            return Task.CompletedTask;
        });

        int codigo = await host.AplicarMigrationsEEncerrarAsync(CancellationToken.None);

        codigo.Should().Be(0);
        execucoes.Should().Be(1);
    }

    [Fact(DisplayName = "No Job ApplyAndExit a falha da etapa encerra com código 1")]
    public async Task Job_FalhaDaEtapaEncerraComUm()
    {
        using IHost host = Montar((_, _) => throw new InvalidOperationException("falha da etapa"));

        int codigo = await host.AplicarMigrationsEEncerrarAsync(CancellationToken.None);

        codigo.Should().Be(1);
    }

    [Fact(DisplayName = "Na subida padrão a etapa roda uma vez e a falha dela derruba a subida")]
    public async Task Subida_ExecutaAEtapaEPropagaAFalha()
    {
        int execucoes = 0;
        using IHost bemSucedido = Montar((_, _) =>
        {
            execucoes++;
            return Task.CompletedTask;
        });
        using IHost comFalha = Montar((_, _) => throw new InvalidOperationException("falha da etapa"));

        await bemSucedido.StartAsync();
        Func<Task> subida = () => comFalha.StartAsync();

        execucoes.Should().Be(1);
        await subida.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "O Job grava o catálogo da aplicação quando o do banco é outro, e não grava de novo ao repetir")]
    public async Task Job_ConciliaOCatalogoDiferente()
    {
        await ExecutarAsync(
            "INSERT INTO autorizacao.implantacoes_das_permissoes (id, impressao, implantada_em) VALUES ({0}, {1}, now())",
            Guid.CreateVersion7(), new string('c', 64));
        using IHost host = Montar(async (_, token) =>
        {
            await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ConciliacaoDoCatalogo>().ExecutarAsync(token);
        });

        int primeiro = await host.AplicarMigrationsEEncerrarAsync(CancellationToken.None);
        long depoisDoPrimeiro = await ContarCatalogosAsync();
        int segundo = await host.AplicarMigrationsEEncerrarAsync(CancellationToken.None);

        primeiro.Should().Be(0);
        segundo.Should().Be(0);
        (await ContarCatalogosAsync()).Should().Be(depoisDoPrimeiro);
        (await ImpressaoAtualAsync()).Should().Be(Fixture.Factory.Services.GetRequiredService<Unifesspa.UniPlus.Authorization.Catalogo.CatalogoDaAplicacao>().Impressao);
    }

    public void Dispose()
    {
        foreach (IServiceScope escopo in _escopos)
        {
            escopo.Dispose();
        }
    }

    private IHost Montar(Func<IServiceProvider, CancellationToken, Task> etapa)
    {
        IServiceScope escopo = Fixture.Factory.Services.CreateScope();
        _escopos.Add(escopo);
        DbContextOptions<MonolitoDbContext> opcoes = escopo.ServiceProvider.GetRequiredService<DbContextOptions<MonolitoDbContext>>();

        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(opcoes);
                services.AddScoped<MonolitoDbContext>();
                services.AddDbContextMigrationsOnStartup<MonolitoDbContext>(etapa);
            })
            .Build();
    }

    private async Task<long> ContarCatalogosAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().ImplantacoesDasPermissoes.LongCountAsync();
    }

    private async Task<string> ImpressaoAtualAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().ImplantacoesDasPermissoes
            .OrderByDescending(c => c.Sequencia).Select(c => c.Impressao).FirstAsync()).Trim();
    }
}
