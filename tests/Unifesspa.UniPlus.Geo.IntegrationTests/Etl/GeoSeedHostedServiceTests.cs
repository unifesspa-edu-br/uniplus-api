namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Seed automático do Geo em desenvolvimento (Story #674, CA-01). Gate duplo (ambiente
/// Development + flag) e disparo só com base vazia, validados com um serviço de
/// importação espião — sem rodar a carga real.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoSeedHostedServiceTests
{
    private readonly GeoPostgisFixture _fixture;

    public GeoSeedHostedServiceTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: em produção, o seed nunca dispara (mesmo com a flag ligada)")]
    public async Task Producao_NaoSemeia()
    {
        await LimparAsync();
        IGeoImportacaoService servico = ServicoEspiao();
        GeoSeedHostedService seed = CriarSeed(servico, ambiente: "Production", flagLigada: true);

        await seed.StartAsync(CancellationToken.None);

        await servico.DidNotReceive().IniciarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-01: em desenvolvimento com a flag desligada, o seed não dispara")]
    public async Task Desenvolvimento_FlagDesligada_NaoSemeia()
    {
        await LimparAsync();
        IGeoImportacaoService servico = ServicoEspiao();
        GeoSeedHostedService seed = CriarSeed(servico, ambiente: "Development", flagLigada: false);

        await seed.StartAsync(CancellationToken.None);

        await servico.DidNotReceive().IniciarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-01: em desenvolvimento com base já populada, o seed não recarrega")]
    public async Task Desenvolvimento_BasePopulada_NaoSemeia()
    {
        await LimparAsync();
        await SemearUmaCidadeAsync();
        IGeoImportacaoService servico = ServicoEspiao();
        GeoSeedHostedService seed = CriarSeed(servico, ambiente: "Development", flagLigada: true);

        await seed.StartAsync(CancellationToken.None);

        await servico.DidNotReceive().IniciarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-01: em desenvolvimento com base vazia e flag ligada, o seed dispara a versão configurada")]
    public async Task Desenvolvimento_BaseVazia_Semeia()
    {
        await LimparAsync();
        IGeoImportacaoService servico = ServicoEspiao();
        GeoSeedHostedService seed = CriarSeed(servico, ambiente: "Development", flagLigada: true, versaoSeed: "202601");

        await seed.StartAsync(CancellationToken.None);

        await servico.Received(1).IniciarAsync("202601", "seed", Arg.Any<CancellationToken>());
    }

    private static IGeoImportacaoService ServicoEspiao()
    {
        IGeoImportacaoService servico = Substitute.For<IGeoImportacaoService>();
        servico.IniciarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Guid>.Success(Guid.CreateVersion7()));
        return servico;
    }

    private GeoSeedHostedService CriarSeed(
        IGeoImportacaoService servico,
        string ambiente,
        bool flagLigada,
        string versaoSeed = "202601")
    {
        ServiceCollection services = new();
        services.AddScoped(_ => _fixture.CreateDbContext());
        services.AddSingleton(servico);
        ServiceProvider provider = services.BuildServiceProvider();

        IHostEnvironment hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(ambiente);

        IOptions<EtlOpcoes> opcoes = Options.Create(new EtlOpcoes
        {
            SeedHabilitado = flagLigada,
            VersaoSeed = versaoSeed,
        });

        return new GeoSeedHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            hostEnvironment,
            opcoes,
            NullLogger<GeoSeedHostedService>.Instance);
    }

    private async Task SemearUmaCidadeAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        FonteEmMemoria fonte = new() { Versao = "202601" };
        fonte.Paises.Add(DadosDne.Pais("BRA", "Brasil", "BR"));
        fonte.Estados.Add(DadosDne.Estado("PA", "Pará"));
        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("PA", "15"));
        fonte.Cidades.Add(DadosDne.Cidade("1500402", "Marabá", "PA"));

        GeoImportadorPaisEstadoCidade importador = new(ctx, NullLogger<GeoImportadorPaisEstadoCidade>.Instance);
        await importador.ImportarAsync(fonte, CancellationToken.None);
    }

    private async Task LimparAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE pais, logradouro_complemento, cep_grande_usuario, geo_importacao_execucao CASCADE");
    }
}
