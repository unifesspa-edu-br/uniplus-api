namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.Net.Http;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchemaRegistryConfig = Confluent.SchemaRegistry.SchemaRegistryConfig;

/// <summary>
/// Extensões DI para o cliente Confluent Schema Registry (Apicurio).
/// </summary>
/// <remarks>
/// <para>
/// API canônica:
/// </para>
/// <code>
/// builder.Services.AddSchemaRegistry(builder.Configuration)
///     .AddSchema("edital_events-value", typeof(EditalPublicadoEvent).Assembly);
/// </code>
/// <para>
/// Comportamento "feature-off": quando <c>SchemaRegistry:Url</c> está vazio em
/// <c>IConfiguration</c>, nada é registrado no DI — APIs sem dependência de schema
/// registry continuam subindo sem erro, e tentativas de resolver
/// <see cref="ISchemaRegistryClient"/> falham fail-fast com
/// <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public static class SchemaRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="ISchemaRegistryClient"/> (Confluent
    /// <c>CachedSchemaRegistryClient</c>) + autenticação configurada +
    /// hosted service idempotente de registro de schemas no startup.
    /// </summary>
    public static SchemaRegistryBuilder AddSchemaRegistry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SchemaRegistrySettings>()
            .Bind(configuration.GetSection(SchemaRegistrySettings.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SchemaRegistrySettings>, SchemaRegistrySettingsValidator>());

        SchemaRegistrySettings settings = configuration
            .GetSection(SchemaRegistrySettings.SectionName)
            .Get<SchemaRegistrySettings>()
            ?? new SchemaRegistrySettings();

        // Feature-off path: URL vazia significa sem Schema Registry. Não registramos
        // ISchemaRegistryClient — quem depender vai falhar fail-fast no DI resolution
        // com mensagem clara, melhor do que comportamento silencioso.
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            return new SchemaRegistryBuilder(services, schemaRegistryEnabled: false);
        }

        // Validação inline cobre o caminho de criação dos clients abaixo —
        // ValidateOnStart só roda em IHost.StartAsync.
        ValidateOptionsResult validation = new SchemaRegistrySettingsValidator()
            .Validate(name: null, settings);
        if (validation.Failed)
        {
            throw new InvalidOperationException(
                $"Configuração SchemaRegistry inválida: {string.Join(" | ", validation.Failures ?? [])}");
        }

        // OAuth provider precisa de HttpClient próprio com timeout configurável —
        // resolvido via IHttpClientFactory para cooperar com Polly/handlers globais.
        bool useOAuthBearer = string.Equals(settings.AuthType, "OAuthBearer", StringComparison.OrdinalIgnoreCase);
        if (useOAuthBearer)
        {
            services.AddHttpClient<OAuthBearerAuthenticationHeaderValueProvider>();
            services.AddSingleton<IAuthenticationHeaderValueProvider>(static sp =>
            {
                IOptions<SchemaRegistrySettings> opts = sp.GetRequiredService<IOptions<SchemaRegistrySettings>>();
                IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
                ILogger<OAuthBearerAuthenticationHeaderValueProvider> logger = sp
                    .GetRequiredService<ILogger<OAuthBearerAuthenticationHeaderValueProvider>>();
                HttpClient client = factory.CreateClient(nameof(OAuthBearerAuthenticationHeaderValueProvider));
                return new OAuthBearerAuthenticationHeaderValueProvider(
                    client,
                    opts.Value.OAuth,
                    logger);
            });
        }

        services.TryAddSingleton<ISchemaRegistryClient>(sp =>
        {
            IOptions<SchemaRegistrySettings> opts = sp.GetRequiredService<IOptions<SchemaRegistrySettings>>();
            SchemaRegistrySettings current = opts.Value;

            SchemaRegistryConfig config = new()
            {
                Url = current.Url,
                MaxCachedSchemas = current.MaxCachedSchemas,
                RequestTimeoutMs = current.RequestTimeoutMs,
            };

            if (string.Equals(current.AuthType, "Basic", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(current.BasicAuthUserInfo))
            {
                config.BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo;
                config.BasicAuthUserInfo = current.BasicAuthUserInfo;
            }

            if (useOAuthBearer)
            {
                IAuthenticationHeaderValueProvider authProvider = sp
                    .GetRequiredService<IAuthenticationHeaderValueProvider>();
                return new CachedSchemaRegistryClient(config, authProvider);
            }

            return new CachedSchemaRegistryClient(config);
        });

        services.AddHostedService<SchemaRegistrationHostedService>();

        return new SchemaRegistryBuilder(services, schemaRegistryEnabled: true);
    }

    /// <summary>
    /// Cria uma instância standalone de <see cref="ISchemaRegistryClient"/> usando
    /// <see cref="SchemaRegistrySettings"/> diretamente — útil para o callback de
    /// configuração do Wolverine (que roda antes do <see cref="IServiceProvider"/>
    /// ter sido construído).
    /// </summary>
    /// <remarks>
    /// O caller é responsável pelo lifetime do cliente retornado. O recomendado é
    /// registrá-lo como singleton no DI via <c>services.AddSingleton(client)</c>
    /// antes de <see cref="AddSchemaRegistry"/> — o <c>TryAddSingleton</c> interno
    /// respeita o registro prévio, evitando duplicação de cache. O hosted service
    /// e o Wolverine routing usam então a mesma instância.
    /// </remarks>
    public static ISchemaRegistryClient CreateClient(
        SchemaRegistrySettings settings,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            throw new InvalidOperationException(
                "SchemaRegistry:Url está vazia — chamando CreateClient sem feature ativa.");
        }

        SchemaRegistryConfig config = new()
        {
            Url = settings.Url,
            MaxCachedSchemas = settings.MaxCachedSchemas,
            RequestTimeoutMs = settings.RequestTimeoutMs,
        };

        if (string.Equals(settings.AuthType, "Basic", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(settings.BasicAuthUserInfo))
        {
            config.BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo;
            config.BasicAuthUserInfo = settings.BasicAuthUserInfo;
            return new CachedSchemaRegistryClient(config);
        }

        if (string.Equals(settings.AuthType, "OAuthBearer", StringComparison.OrdinalIgnoreCase))
        {
            // HttpClient standalone — cliente é singleton (lifetime = host),
            // ownership transferido para o auth provider (que dispõe via ownsHttpClient).
            // O auth provider, por sua vez, vive enquanto o CachedSchemaRegistryClient
            // vive (referência forte). Caller registra o cliente como singleton no DI;
            // shutdown do host descarta toda a cadeia.
            HttpClient httpClient = new();
#pragma warning disable CA2000 // analisador não rastreia ownership pelo flag ownsHttpClient nem a posse pelo CachedSchemaRegistryClient.
            OAuthBearerAuthenticationHeaderValueProvider authProvider = new(
                httpClient,
                ownsHttpClient: true,
                settings.OAuth,
                loggerFactory.CreateLogger<OAuthBearerAuthenticationHeaderValueProvider>());
#pragma warning restore CA2000
            return new CachedSchemaRegistryClient(config, authProvider);
        }

        return new CachedSchemaRegistryClient(config);
    }
}

/// <summary>
/// Builder fluente para registrar <see cref="SchemaRegistration"/>s do módulo após
/// <see cref="SchemaRegistryServiceCollectionExtensions.AddSchemaRegistry"/>.
/// </summary>
public sealed class SchemaRegistryBuilder
{
    private readonly IServiceCollection services;
    private readonly bool schemaRegistryEnabled;

    internal SchemaRegistryBuilder(IServiceCollection services, bool schemaRegistryEnabled)
    {
        this.services = services;
        this.schemaRegistryEnabled = schemaRegistryEnabled;
    }

    /// <summary>
    /// Adiciona um schema Avro a ser registrado/confirmado no Schema Registry no startup.
    /// </summary>
    /// <param name="subject">Subject Confluent SR (e.g. <c>edital_events-value</c>).</param>
    /// <param name="schemaResourceName">Nome canônico do embedded resource.</param>
    /// <param name="resourceAssembly">Assembly que contém o resource.</param>
    public SchemaRegistryBuilder AddSchema(
        string subject,
        string schemaResourceName,
        System.Reflection.Assembly resourceAssembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaResourceName);
        ArgumentNullException.ThrowIfNull(resourceAssembly);

        if (!schemaRegistryEnabled)
        {
            // Feature-off: não registra a SchemaRegistration no DI — o hosted service
            // não foi adicionado, e o cliente também não. AddSchema vira no-op.
            return this;
        }

        services.AddSingleton(new SchemaRegistration(subject, schemaResourceName, resourceAssembly));
        return this;
    }
}
