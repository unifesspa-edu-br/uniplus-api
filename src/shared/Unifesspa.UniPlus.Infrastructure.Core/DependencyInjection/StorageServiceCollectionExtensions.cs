namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Minio;

using Storage;

/// <summary>
/// Registra <see cref="IMinioClient"/> e <see cref="IStorageService"/> no container DI a partir
/// da seção <c>Storage</c> do <see cref="IConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Padrão alinhado com <see cref="Cors.CorsConfiguration"/> e
/// <see cref="Authentication.OidcAuthenticationConfiguration"/>: validação leniente em Development,
/// fail-fast fora de Development quando <c>Storage:Endpoint</c>, <c>Storage:AccessKey</c> ou
/// <c>Storage:SecretKey</c> estão vazios.
/// </para>
/// <para>
/// <see cref="IMinioClient"/> é registrado como <em>singleton</em> — o cliente mantém um pool
/// HTTP interno reutilizável entre requests. <see cref="IStorageService"/> é <em>scoped</em> por
/// request, conforme padrão do projeto para serviços de Infrastructure que abstraem clientes
/// remotos. <see cref="MinioStorageService"/> é stateless, então scope vs. singleton é
/// equivalente em runtime — manter scoped preserva a opção de injetar serviços scoped no futuro
/// (correlation id, user context) sem mudança de contrato.
/// </para>
/// </remarks>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="StorageOptions"/>, <see cref="IMinioClient"/> e
    /// <see cref="IStorageService"/>. Deve ser chamado uma única vez por aplicação,
    /// junto com os demais <c>AddUniPlus*</c> em <c>Program.cs</c>.
    /// </summary>
    /// <param name="services">A coleção de serviços.</param>
    /// <param name="configuration">Configuração da aplicação (lê seção <c>Storage</c>).</param>
    /// <param name="environment">Ambiente de hospedagem — controla rigor da validação.</param>
    /// <returns>A própria <paramref name="services"/> para encadeamento fluente.</returns>
    /// <exception cref="ArgumentNullException">Algum dos parâmetros é <see langword="null"/>.</exception>
    public static IServiceCollection AddUniPlusStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment()
                    || (!string.IsNullOrWhiteSpace(options.Endpoint)
                        && !string.IsNullOrWhiteSpace(options.AccessKey)
                        && !string.IsNullOrWhiteSpace(options.SecretKey)),
                "Storage:Endpoint, Storage:AccessKey and Storage:SecretKey must be configured outside Development.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.Endpoint)
                    || (!options.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        && !options.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)),
                "Storage:Endpoint must be host:port without scheme — control HTTPS via Storage:UseSSL.")
            .ValidateOnStart();

        services.AddSingleton<IMinioClient>(sp =>
        {
            StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

            IMinioClient client = new MinioClient()
                .WithEndpoint(opts.Endpoint)
                .WithCredentials(opts.AccessKey, opts.SecretKey)
                .WithSSL(opts.UseSSL);

            if (!string.IsNullOrWhiteSpace(opts.Region))
            {
                client = client.WithRegion(opts.Region);
            }

            return client.Build();
        });

        services.AddScoped<IStorageService, MinioStorageService>();

        return services;
    }
}
