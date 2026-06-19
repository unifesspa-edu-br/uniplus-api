namespace Unifesspa.UniPlus.Geo.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Infrastructure.Caching;
using Unifesspa.UniPlus.Geo.Infrastructure.Cep;
using Unifesspa.UniPlus.Geo.Infrastructure.Observability;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Bulk;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Registra a infraestrutura do módulo Geo (DbContext + interceptors
/// transversais soft delete + audit). O <c>GeoDbContext</c> ativa o plugin
/// NetTopologySuite do Npgsql via o hook do <c>UseUniPlusNpgsqlConventions</c>
/// (ADR-0091) — paridade com o design-time factory. Repositórios e readers
/// entram nas Stories de domínio/API.
/// </summary>
public static class GeoInfrastructureRegistration
{
    private const string ConnectionStringName = "GeoDb";

    public static IServiceCollection AddGeoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<GeoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<GeoDbContext>(
                serviceProvider,
                ConnectionStringName,
                npgsql => npgsql.UseNetTopologySuite()));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<GeoDbContext>());

        // Readers read-side da API pública de reference data (Story #675): listagem
        // por cursor + detalhe por chave natural. Só expõem o que vigente (ADR-0092).
        services.AddScoped<IEstadoReader, EstadoReader>();
        services.AddScoped<ICidadeReader, CidadeReader>();
        services.AddScoped<IDistritoReader, DistritoReader>();
        services.AddScoped<IBairroReader, BairroReader>();
        services.AddScoped<ILogradouroReader, LogradouroReader>();

        // Proximidade geoespacial (Story #678): filtro ST_DWithin (índice GIST) +
        // ordenação ST_Distance sobre geography (ADR-0091). Só reference data vigente.
        services.AddScoped<IGeoProximidadeReader, GeoProximidadeReader>();

        // Lookup de CEP (Story #676): reader da cascata (logradouro → grande usuário →
        // faixa) + resolver com cache-aside por selo de versão (ADR-0090/0092). O
        // Lazy<ICacheService> difere o Connect do Redis para o resolver degradar ao
        // banco quando o cache está fora (espelha o Lazy do ETL, #674).
        services.AddScoped<ICepReader, CepReader>();
        services.AddScoped<ICepResolver, CepResolver>();
        services.AddOptions<GeoCepCacheOptions>();
        services.AddOptions<GeoCepLookupOptions>();
        services.AddScoped(sp => new Lazy<ICacheService>(sp.GetRequiredService<ICacheService>));
        // Memoização em processo do selo de versão vigente do cache de CEP (#703):
        // 1 round-trip ao Redis no hot path em vez de 2. AddMemoryCache é idempotente.
        services.AddMemoryCache();

        // ETL DNE (ADR-0092) — serviços de carga de reference data. O gatilho (seed
        // dev / endpoint admin) e a fonte concreta de produção entram na Story #674.
        // Topo da hierarquia (País/Estado/Cidade, #672):
        services.AddScoped<IGeoImportador, GeoImportadorPaisEstadoCidade>();

        // Folhas (Distrito/Bairro/Logradouro + satélites, #673): o orquestrador compõe
        // o upsert de Distrito/Bairro e o COPY em lote dos logradouros.
        services.AddScoped<GeoImportadorDistritoBairro>();
        services.AddScoped<LogradouroCopyImporter>();
        services.AddScoped<IGeoImportadorLocalidades, GeoImportadorLocalidades>();

        // Atualização periódica (#674): orquestrador (uma instância scoped por escopo,
        // exposta às duas portas — API e worker), fila in-process, fonte por versão,
        // invalidação do cache de CEP por selo de versão e métricas do ETL.
        services.AddScoped<GeoEtlOrquestrador>();
        services.AddScoped<IGeoImportacaoService>(sp => sp.GetRequiredService<GeoEtlOrquestrador>());
        services.AddScoped<IGeoImportacaoExecutor>(sp => sp.GetRequiredService<GeoEtlOrquestrador>());
        services.AddSingleton<IGeoImportacaoFila, GeoImportacaoFila>();
        services.AddSingleton<IGeoFonteDadosFactory, DneStagingFonteFactory>();
        services.AddScoped<IGeoCepCacheInvalidador, RedisGeoCepCacheInvalidador>();
        // Lazy para o orquestrador: difere a resolução da cadeia Redis (IConnectionMultiplexer
        // conecta na construção) até o momento de selar — só no worker, nunca no disparo.
        services.AddScoped(sp => new Lazy<IGeoCepCacheInvalidador>(sp.GetRequiredService<IGeoCepCacheInvalidador>));
        services.AddSingleton<GeoEtlMetrics>();

        return services;
    }

    /// <summary>
    /// Registra os gatilhos hospedados do ETL (#674): a reconciliação de órfãs no startup
    /// (#695), o <c>BackgroundService</c> que executa as cargas enfileiradas e o seed de
    /// desenvolvimento. <strong>Deve ser chamado no Program APÓS
    /// <c>AddDbContextMigrationsOnStartup&lt;GeoDbContext&gt;()</c></strong> — como
    /// <c>HostOptions.ServicesStartConcurrently</c> é <see langword="false"/>, a ordem de
    /// registro é a ordem de start: migrations → reconciliação → worker → seed. A
    /// reconciliação (cujo <c>StartAsync</c> é aguardado) conclui antes de o seed/HTTP
    /// aceitarem disparos, e tanto ela quanto o seed tocam o banco (a tabela precisa já existir).
    /// </summary>
    public static IServiceCollection AddGeoEtlGatilhos(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<EtlOpcoes>().Bind(configuration.GetSection(EtlOpcoes.SectionName));

        EtlOpcoes opcoes = configuration.GetSection(EtlOpcoes.SectionName).Get<EtlOpcoes>() ?? new EtlOpcoes();

        if (opcoes.WorkerHabilitado)
        {
            // Reconciliação de órfãs ANTES do worker e do seed (#695): hosted service cujo
            // StartAsync é aguardado pelo host, liberando o índice único parcial de execuções
            // EmAndamento abandonadas antes que qualquer disparo possa colidir nele.
            services.AddHostedService<GeoReconciliacaoStartupHostedService>();
            services.AddHostedService<GeoImportacaoBackgroundService>();
        }

        // Seed só em Development e com a flag ligada (default off) — os testes rodam como
        // Development mas não ligam a flag, então nunca semeiam.
        if (environment.IsDevelopment() && opcoes.SeedHabilitado)
        {
            services.AddHostedService<GeoSeedHostedService>();
        }

        return services;
    }
}
