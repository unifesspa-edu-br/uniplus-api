namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Diagnostics.CodeAnalysis;

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
            return BuildClient(opts, opts.Endpoint, opts.UseSSL);
        });

        // Cliente separado, keyed, usado só para ASSINAR URLs pre-assinadas
        // devolvidas a clientes externos (browser fora da rede Docker/cluster).
        // A assinatura SigV4 inclui o header Host — reescrever a URL depois de
        // assinada com o endpoint interno invalidaria a assinatura, então este
        // cliente assina do zero com PublicEndpoint. Sem PublicEndpoint
        // configurado, reusa a MESMA instância do cliente interno (nenhum
        // custo extra; comportamento idêntico ao anterior).
        services.AddKeyedSingleton<IMinioClient>(StoragePublicClientKey, (sp, _) =>
        {
            StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.PublicEndpoint) || opts.PublicEndpoint == opts.Endpoint)
            {
                return sp.GetRequiredService<IMinioClient>();
            }

            return BuildClient(opts, opts.PublicEndpoint, opts.PublicUseSSL ?? opts.UseSSL);
        });

        // IHttpClientFactory — usado por MinioStorageService.DownloadLimitadoAsync
        // para o GET com Range via URL pre-assinada (a API de alto nível do SDK
        // MinIO não suporta range parcial). AddHttpClient() é idempotente.
        services.AddHttpClient();
        services.AddScoped<IStorageService, MinioStorageService>();

        return services;
    }

    /// <summary>Chave do <see cref="IMinioClient"/> keyed usado para assinar URLs devolvidas a clientes externos.</summary>
    public const string StoragePublicClientKey = "storage-public";

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "O cliente é devolvido ao caller (factory de DI), que o registra como singleton — o container é o dono do descarte, não este método.")]
    private static IMinioClient BuildClient(StorageOptions opts, string endpoint, bool useSSL)
    {
        IMinioClient client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(opts.AccessKey, opts.SecretKey)
            .WithSSL(useSSL);

        if (!string.IsNullOrWhiteSpace(opts.Region))
        {
            client = client.WithRegion(opts.Region);
        }

        return client.Build();
    }
}
