namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.Diagnostics.CodeAnalysis;
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

        // OAuth provider — registro defensivo via IHttpClientFactory cobre o caso em que
        // o caller usa apenas AddSchemaRegistry sem chamar CreateClient antes (e.g.
        // futuro módulo só com hosted service de registro). Quando CreateClient é
        // invocado primeiro (path canônico em Selecao.API), ele já registrou a
        // instância concreta do auth provider — TryAdd respeita.
        bool useOAuthBearer = string.Equals(settings.AuthType, "OAuthBearer", StringComparison.OrdinalIgnoreCase);
        if (useOAuthBearer)
        {
            services.AddHttpClient<OAuthBearerAuthenticationHeaderValueProvider>();
            services.TryAddSingleton<IAuthenticationHeaderValueProvider>(static sp =>
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
    /// <para>
    /// O caller registra o cliente retornado como singleton no DI; <see cref="AddSchemaRegistry"/>
    /// usa <c>TryAddSingleton</c> e respeita o registro prévio, evitando duplicação de cache.
    /// Hosted service e Wolverine routing usam então a mesma instância.
    /// </para>
    /// <para>
    /// <b>Lifecycle de dependências standalone (#360):</b> quando <c>AuthType=OAuthBearer</c>,
    /// o método cria internamente um <see cref="OAuthBearerAuthenticationHeaderValueProvider"/>
    /// (que possui um <see cref="HttpClient"/> próprio com <c>ownsHttpClient: true</c>).
    /// O <see cref="CachedSchemaRegistryClient"/> da Confluent <b>não dispõe</b> custom auth
    /// providers em seu próprio <c>Dispose</c>. Para evitar leak de socket/handler em
    /// processos que sobem/derrubam hosts repetidamente (e.g. <c>WebApplicationFactory</c> em
    /// IntegrationTests com Apicurio Testcontainer), o auth provider é registrado em
    /// <paramref name="services"/> como singleton — Microsoft.Extensions.DependencyInjection
    /// dispõe singletons <see cref="IDisposable"/> automaticamente no shutdown do host.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "HttpClient é owned pelo OAuthBearerAuthenticationHeaderValueProvider (ownsHttpClient: true). O auth provider é instanciado eager para captura no closure do callback Wolverine UseWolverine (que roda antes de builder.Build() — IServiceProvider não disponível) e seu lifecycle é gerenciado por OAuthBearerAuthProviderDisposeHostedService registrado em IServiceCollection: o container instancia o hosted service (e o dispõe no shutdown), e o hosted service dispõe explicitamente o auth provider em StopAsync. AddSingleton(instance) não dispõe — apenas registrations via tipo/factory dispõem (Microsoft DI guidelines, roslyn-analyzers#5447).")]
    public static ISchemaRegistryClient CreateClient(
        SchemaRegistrySettings settings,
        ILoggerFactory loggerFactory,
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(services);

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
            // HttpClient standalone — ownership transferido para o auth provider via
            // ownsHttpClient: true. O provider, por sua vez, é registrado no DI
            // abaixo para que o IHost dispõe via lifecycle de singleton IDisposable
            // — necessário porque CachedSchemaRegistryClient.Dispose não dispõe
            // custom auth providers (Codex P2 na review do PR #359, issue #360).
            HttpClient httpClient = new();
            OAuthBearerAuthenticationHeaderValueProvider authProvider = new(
                httpClient,
                ownsHttpClient: true,
                settings.OAuth,
                loggerFactory.CreateLogger<OAuthBearerAuthenticationHeaderValueProvider>());

            // Registrations:
            // - Singleton(instance) torna o auth provider resolvable via DI (mesma
            //   instância usada eager no Wolverine routing). NÃO leva a dispose
            //   automático (container não dispõe instâncias pré-criadas).
            // - AddHostedService garante dispose: container instancia o hosted service
            //   e o dispõe no shutdown; o hosted service chama Dispose() no provider.
            //   IServiceCollection injeta authProvider via DI no construtor.
            services.AddSingleton(authProvider);
            services.AddSingleton<IAuthenticationHeaderValueProvider>(authProvider);
            services.AddHostedService<OAuthBearerAuthProviderDisposeHostedService>();

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
