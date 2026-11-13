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

        RegistrarOpcoes(services, configuration, environment);
        RegistrarClientesDoMinio(services);
        RegistrarServicoDeArmazenamento(services);

        return services;
    }

    /// <summary>
    /// Cria um <see cref="IStorageService"/> com credenciais próprias, FORA do container, para o armazenamento que não
    /// pode usar a credencial geral de <see cref="AddUniPlusStorage"/> (que enxerga todos os buckets). Não registra
    /// nenhum <see cref="IStorageService"/> adicional: um segundo registro seria candidato à resolução por tipo de
    /// qualquer consumidor sem chave. Exige que <see cref="AddUniPlusStorage"/> tenha sido chamado.
    /// </summary>
    /// <param name="provider">Provedor de onde saem o <see cref="IHttpClientFactory"/> e o <see cref="TimeProvider"/>.</param>
    /// <param name="opcoes">Opções completas do armazenamento, já com a credencial própria.</param>
    public static IStorageService CriarStorageComCredencialPropria(IServiceProvider provider, StorageOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(opcoes);

        IMinioClient interno = BuildClient(opcoes, opcoes.Endpoint, opcoes.UseSSL);
        return new MinioStorageService(
            interno,
            ClienteDeAssinatura(opcoes, interno),
            provider.GetRequiredService<IHttpClientFactory>(),
            Options.Create(opcoes),
            provider.GetRequiredService<TimeProvider>());
    }

    /// <summary>
    /// Cria o <see cref="IStorageService"/> de um módulo com a credencial própria dele: o endpoint, o TLS, a região e o
    /// endereço público são os de <c>Storage</c>, e o bucket nunca é criado — é provisionado à parte, com política de
    /// acesso mínima. Exige que <see cref="AddUniPlusStorage"/> tenha sido chamado.
    /// </summary>
    public static IStorageService CriarStorageComCredencialPropria(IServiceProvider provider, string accessKey, string secretKey)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        StorageOptions geral = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

        return CriarStorageComCredencialPropria(provider, OpcoesComCredencialPropria(geral, accessKey, secretKey));
    }

    /// <summary>Chave do <see cref="IMinioClient"/> keyed usado para assinar URLs devolvidas a clientes externos.</summary>
    public const string StoragePublicClientKey = "storage-public";

    /// <summary>
    /// Chave do <see cref="IMinioClient"/> keyed usado para chamadas internas (dentro da rede
    /// Docker/cluster) — <c>Storage:Endpoint</c>. Keyed mesmo sendo o único consumidor "normal"
    /// porque um consumidor com este e o <see cref="StoragePublicClientKey"/> como dois
    /// parâmetros de construtor do mesmo tipo IMinioClient precisa que AMBOS sejam
    /// inequivocamente identificados.
    /// </summary>
    public const string StorageInternalClientKey = "storage-internal";

    private static void RegistrarOpcoes(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment() || CredenciaisConfiguradas(options),
                "Storage:Endpoint, Storage:AccessKey and Storage:SecretKey must be configured outside Development.")
            .Validate(
                static options => SemEsquema(options.Endpoint),
                "Storage:Endpoint must be host:port without scheme — control HTTPS via Storage:UseSSL.")
            .Validate(
                static options => SemEsquema(options.PublicEndpoint),
                "Storage:PublicEndpoint must be host:port without scheme — control HTTPS via Storage:PublicUseSSL.")
            .ValidateOnStart();
    }

    private static bool CredenciaisConfiguradas(StorageOptions options) =>
        !string.IsNullOrWhiteSpace(options.Endpoint)
        && !string.IsNullOrWhiteSpace(options.AccessKey)
        && !string.IsNullOrWhiteSpace(options.SecretKey);

    private static bool SemEsquema(string? endereco) =>
        string.IsNullOrWhiteSpace(endereco)
        || (!endereco.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !endereco.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Os dois clientes são registrados KEYED, inclusive o interno, que não tem motivo funcional para ser keyed
    /// sozinho: um consumidor com DOIS parâmetros de construtor do MESMO tipo (<see cref="IMinioClient"/>), um deles
    /// via <c>[FromKeyedServices]</c> e o outro sem atributo, recebe a instância keyed nos dois — o container não
    /// distingue o registro sem chave. Nomear os dois evita a ambiguidade na raiz.
    /// </summary>
    /// <remarks>
    /// O cliente público assina as URLs pre-assinadas devolvidas a clientes externos (browser fora da rede
    /// Docker/cluster). A assinatura SigV4 inclui o header Host, então reescrever a URL depois de assinada com o
    /// endpoint interno a invalidaria: o cliente assina do zero com <c>PublicEndpoint</c>/<c>PublicUseSSL</c>. Ele
    /// reusa a instância do interno só quando host E esquema batem.
    /// </remarks>
    private static void RegistrarClientesDoMinio(IServiceCollection services)
    {
        services.AddKeyedSingleton<IMinioClient>(StorageInternalClientKey, (sp, _) =>
        {
            StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return BuildClient(opts, opts.Endpoint, opts.UseSSL);
        });

        services.AddKeyedSingleton<IMinioClient>(StoragePublicClientKey, (sp, _) =>
        {
            StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return ClienteDeAssinatura(opts, sp.GetRequiredKeyedService<IMinioClient>(StorageInternalClientKey));
        });
    }

    /// <summary>
    /// O <see cref="IHttpClientFactory"/> serve ao GET com Range via URL pre-assinada de
    /// <see cref="MinioStorageService"/>, que a API de alto nível do SDK não suporta. A assinatura da cópia para o
    /// acervo público carimba o instante da requisição, com o <see cref="TimeProvider"/> registrado pelo host.
    /// </summary>
    private static void RegistrarServicoDeArmazenamento(IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<IStorageService, MinioStorageService>();
    }

    private static StorageOptions OpcoesComCredencialPropria(StorageOptions geral, string accessKey, string secretKey) =>
        new()
        {
            Endpoint = geral.Endpoint,
            UseSSL = geral.UseSSL,
            Region = geral.Region,
            PublicEndpoint = geral.PublicEndpoint,
            PublicUseSSL = geral.PublicUseSSL,
            AccessKey = accessKey,
            SecretKey = secretKey,
            CriaBucketAusente = false,
        };

    // O cliente de assinatura só é outro quando o endpoint público difere do interno em host ou esquema (HTTP dentro do
    // cluster, HTTPS no mesmo hostname por fora): a assinatura SigV4 inclui o Host.
    private static IMinioClient ClienteDeAssinatura(StorageOptions opts, IMinioClient interno)
    {
        bool endpointIgual = string.IsNullOrWhiteSpace(opts.PublicEndpoint) || opts.PublicEndpoint == opts.Endpoint;
        bool sslIgual = opts.PublicUseSSL is null || opts.PublicUseSSL == opts.UseSSL;
        if (endpointIgual && sslIgual)
        {
            return interno;
        }

        string endpointResolvido = string.IsNullOrWhiteSpace(opts.PublicEndpoint) ? opts.Endpoint : opts.PublicEndpoint;
        return BuildClient(opts, endpointResolvido, opts.PublicUseSSL ?? opts.UseSSL);
    }

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
