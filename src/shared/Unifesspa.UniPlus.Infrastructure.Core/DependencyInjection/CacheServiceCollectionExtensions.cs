namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// Registra <see cref="IConnectionMultiplexer"/> e <see cref="ICacheService"/> no container DI
/// a partir da seção <c>Redis</c> do <see cref="IConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Padrão alinhado com <see cref="StorageServiceCollectionExtensions.AddUniPlusStorage"/>:
/// validação leniente em Development, fail-fast fora quando <c>Redis:ConnectionString</c>
/// está vazio.
/// </para>
/// <para>
/// <see cref="IConnectionMultiplexer"/> é registrado como <em>singleton</em> — o
/// <c>StackExchange.Redis</c> mantém um pool TCP interno multi-plexado e é projetado para
/// reutilização global no processo. <see cref="ICacheService"/> é <em>scoped</em> alinhado
/// com o pattern dos demais serviços de Infrastructure.
/// </para>
/// </remarks>
public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="RedisOptions"/>, <see cref="IConnectionMultiplexer"/> e
    /// <see cref="ICacheService"/>. Deve ser chamado uma única vez por aplicação,
    /// junto com os demais <c>AddUniPlus*</c> em <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddUniPlusCache(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment()
                    || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Redis:ConnectionString must be configured outside Development.")
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            RedisOptions opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(opts.ConnectionString);
        });

        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }
}
