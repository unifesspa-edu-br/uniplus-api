namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.Net.Http;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
    /// <remarks>
    /// Feature-off: URL vazia significa sem Schema Registry. O <see cref="ISchemaRegistryClient"/> não é registrado, e
    /// quem depender dele falha fail-fast na resolução, com mensagem clara, em vez de comportamento silencioso.
    /// </remarks>
    public static SchemaRegistryBuilder AddSchemaRegistry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        SchemaRegistrySettings settings = RegistrarOpcoes(services, configuration);
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            return new SchemaRegistryBuilder(services, schemaRegistryEnabled: false);
        }

        ValidarOpcoes(settings);
        RegistrarClienteEHospedagemDoRegistro(services, UsaOAuthBearer(settings));

        return new SchemaRegistryBuilder(services, schemaRegistryEnabled: true);
    }

    /// <summary>
    /// Cria o <see cref="ISchemaRegistryClient"/> standalone a partir de <see cref="SchemaRegistrySettings"/> — útil
    /// para o callback de configuração do Wolverine, que roda antes de o <see cref="IServiceProvider"/> existir.
    /// </summary>
    /// <remarks>
    /// O caller registra o cliente devolvido como singleton; <see cref="AddSchemaRegistry"/> usa
    /// <c>TryAddSingleton</c> e respeita o registro prévio, evitando duplicação de cache. O hosted service e o routing
    /// do Wolverine usam então a mesma instância. A posse do cliente e do provider OAuth fica com o
    /// <see cref="SchemaRegistryDoBootstrap"/>, registrado em <paramref name="services"/> para o host descartá-lo no
    /// encerramento. O <paramref name="relogio"/> é o mesmo registrado no container pelo host: o bootstrap roda antes
    /// do <c>Build</c>, então um relógio trocado depois, nos testes, não chega ao provider OAuth.
    /// </remarks>
    public static ISchemaRegistryClient CreateClient(
        SchemaRegistrySettings settings,
        ILoggerFactory loggerFactory,
        IServiceCollection services,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(relogio);
        ExigirUrl(settings);

        SchemaRegistryDoBootstrap dono = new(settings, loggerFactory, relogio);
        RegistrarPosse(services, dono);

        return dono.Cliente;
    }

    internal static bool UsaOAuthBearer(SchemaRegistrySettings settings) =>
        string.Equals(settings.AuthType, "OAuthBearer", StringComparison.OrdinalIgnoreCase);

    private static bool UsaBasicComCredenciais(SchemaRegistrySettings settings) =>
        string.Equals(settings.AuthType, "Basic", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(settings.BasicAuthUserInfo);

    internal static SchemaRegistryConfig ConfiguracaoDoCliente(SchemaRegistrySettings settings)
    {
        SchemaRegistryConfig configuracao = new()
        {
            Url = settings.Url,
            MaxCachedSchemas = settings.MaxCachedSchemas,
            RequestTimeoutMs = settings.RequestTimeoutMs,
        };

        if (UsaBasicComCredenciais(settings))
        {
            configuracao.BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo;
            configuracao.BasicAuthUserInfo = settings.BasicAuthUserInfo;
        }

        return configuracao;
    }

    private static SchemaRegistrySettings RegistrarOpcoes(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SchemaRegistrySettings>()
            .Bind(configuration.GetSection(SchemaRegistrySettings.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SchemaRegistrySettings>, SchemaRegistrySettingsValidator>());

        return configuration
            .GetSection(SchemaRegistrySettings.SectionName)
            .Get<SchemaRegistrySettings>()
            ?? new SchemaRegistrySettings();
    }

    /// <summary>
    /// A validação inline cobre o caminho de criação dos clients: o <c>ValidateOnStart</c> só roda em
    /// <c>IHost.StartAsync</c>.
    /// </summary>
    private static void ValidarOpcoes(SchemaRegistrySettings settings)
    {
        ValidateOptionsResult validation = new SchemaRegistrySettingsValidator()
            .Validate(name: null, settings);
        if (validation.Failed)
        {
            throw new InvalidOperationException(
                $"Configuração SchemaRegistry inválida: {string.Join(" | ", validation.Failures ?? [])}");
        }
    }

    private static void ExigirUrl(SchemaRegistrySettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            throw new InvalidOperationException(
                "SchemaRegistry:Url está vazia — chamando CreateClient sem feature ativa.");
        }
    }

    private static void RegistrarClienteEHospedagemDoRegistro(IServiceCollection services, bool usaOAuthBearer)
    {
        if (usaOAuthBearer)
        {
            RegistrarAutenticacaoOAuthBearer(services);
        }

        services.TryAddSingleton<ISchemaRegistryClient>(sp => CriarClienteDoContainer(sp, usaOAuthBearer));
        services.AddHostedService<SchemaRegistrationHostedService>();
    }

    /// <summary>
    /// Registro defensivo, via <see cref="IHttpClientFactory"/>, para o caller que usa só
    /// <see cref="AddSchemaRegistry"/>, sem o cliente standalone. Quando <see cref="CreateClient"/> vem antes (caminho
    /// canônico do Seleção), o provider dele já está registrado e o <c>TryAdd</c> o respeita. O provider resolve o
    /// <see cref="TimeProvider"/> do host por <c>GetRequiredService</c>, fail-fast.
    /// </summary>
    private static void RegistrarAutenticacaoOAuthBearer(IServiceCollection services)
    {
        services.AddHttpClient<OAuthBearerAuthenticationHeaderValueProvider>();
        services.TryAddSingleton<IAuthenticationHeaderValueProvider>(AutenticacaoOAuthBearerDoContainer);
    }

    private static OAuthBearerAuthenticationHeaderValueProvider AutenticacaoOAuthBearerDoContainer(IServiceProvider sp) =>
        new(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OAuthBearerAuthenticationHeaderValueProvider)),
            sp.GetRequiredService<IOptions<SchemaRegistrySettings>>().Value.OAuth,
            sp.GetRequiredService<ILogger<OAuthBearerAuthenticationHeaderValueProvider>>(),
            sp.GetRequiredService<TimeProvider>());

    private static CachedSchemaRegistryClient CriarClienteDoContainer(IServiceProvider sp, bool usaOAuthBearer)
    {
        SchemaRegistryConfig configuracao = ConfiguracaoDoCliente(sp.GetRequiredService<IOptions<SchemaRegistrySettings>>().Value);

        return usaOAuthBearer
            ? new CachedSchemaRegistryClient(configuracao, sp.GetRequiredService<IAuthenticationHeaderValueProvider>())
            : new CachedSchemaRegistryClient(configuracao);
    }

    /// <summary>
    /// O dono entra como <see cref="IHostedService"/> por factory (ver <see cref="SchemaRegistryDoBootstrap"/>), e o
    /// provider OAuth, quando há, como instância que não transfere a posse: o <c>TryAdd</c> do
    /// <see cref="AddSchemaRegistry"/> então não cria um segundo provider com cache de token próprio.
    /// </summary>
    private static void RegistrarPosse(IServiceCollection services, SchemaRegistryDoBootstrap dono)
    {
        services.AddSingleton<IHostedService>(_ => dono);
        if (dono.Autenticacao is { } autenticacao)
        {
            services.AddSingleton(autenticacao);
        }
    }
}

/// <summary>
/// Builder fluente para registrar <see cref="SchemaRegistration"/>s do módulo após
/// <see cref="SchemaRegistryServiceCollectionExtensions.AddSchemaRegistry"/>.
/// </summary>
public sealed class SchemaRegistryBuilder
{
    private readonly IServiceCollection _services;
    private readonly bool _schemaRegistryEnabled;

    internal SchemaRegistryBuilder(IServiceCollection services, bool schemaRegistryEnabled)
    {
        _services = services;
        _schemaRegistryEnabled = schemaRegistryEnabled;
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

        if (!_schemaRegistryEnabled)
        {
            // Feature-off: não registra a SchemaRegistration no DI — o hosted service
            // não foi adicionado, e o cliente também não. AddSchema vira no-op.
            return this;
        }

        _services.AddSingleton(new SchemaRegistration(subject, schemaResourceName, resourceAssembly));
        return this;
    }
}
