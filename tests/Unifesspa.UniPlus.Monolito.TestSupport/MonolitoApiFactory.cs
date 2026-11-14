namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// base das suítes de integração do monólito modular: sobe a <b>API UniPlus</b>
/// (composition root) — o único entry point dos módulos internos, que agora são
/// class libraries. A connection string <c>UniPlusDb</c> aponta para o banco
/// <c>uniplus</c> (schema-por-módulo).
/// </summary>
/// <remarks>
/// <para>Entry point via <see cref="HostAssemblyMarker"/>: o host co-hospeda os
/// módulos e há vários <c>Program</c> no namespace global durante a transição —
/// o marcador do assembly do host evita a ambiguidade CS0433.</para>
/// <para><see cref="ICacheService"/> é substituído por
/// <see cref="FakeInMemoryCacheService"/> (sem Redis). Wolverine é habilitado
/// conforme <paramref name="wolverineEnabled"/>: suítes HTTP-only (OpenAPI, auth)
/// sobem sem Postgres (<c>false</c>); suítes de endpoint com outbox provisionam
/// Postgres e usam <c>true</c>.</para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative usado como estado de fixture entre assemblies de teste.")]
public class MonolitoApiFactory : ApiFactoryBase<HostAssemblyMarker>
{
    /// <summary>Endereço da Configuração que os links dos avisos do pedido levam.</summary>
    public const string EnderecoDaConfiguracao = "https://uniplus.test/configuracao";

    private readonly string _connectionString;
    private readonly bool _wolverineEnabled;

    /// <param name="connectionString">
    /// Connection string do host (<c>UniPlusDb</c>, a única). Em suítes HTTP-only
    /// (sem Postgres) basta um valor não-vazio (o DbContext é resolvido lazy e
    /// nunca usado); em suítes de endpoint, a do Postgres efêmero.
    /// </param>
    /// <param name="wolverineEnabled">
    /// <c>true</c> mantém o runtime Wolverine + migrations on startup (exige
    /// Postgres); <c>false</c> remove ambos (pipeline HTTP puro).
    /// </param>
    public MonolitoApiFactory(string connectionString, bool wolverineEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _wolverineEnabled = wolverineEnabled;
    }

    protected override bool DisableWolverineRuntimeForTests => !_wolverineEnabled;

    protected sealed override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides()
    {
        // Dicionário (não List) porque AddInMemoryCollection lança em chave duplicada:
        // o MemoryConfigurationProvider faz Dictionary.Add por par. Coletar aqui com
        // indexer garante chave única e semântica last-wins — overrides da subclasse
        // (aplicados depois) vencem os defaults sem colidir.
        Dictionary<string, string?> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:UniPlusDb"] = _connectionString,
            ["Autorizacao:EnderecoDaConfiguracao"] = EnderecoDaConfiguracao,
            // Valor só para a validação da subida: o registro real não é construído, porque o de teste o substitui.
            ["RegistroDeAcesso:Kafka:BootstrapServers"] = "localhost:9092",
            ["ArquivosDoCandidato:AccessKey"] = "teste",
            ["ArquivosDoCandidato:SecretKey"] = "teste",
            ["RabbitMq:Uri"] = Environment.GetEnvironmentVariable("RabbitMq__Uri") is { Length: > 0 } uri ? uri : "amqp://guest:guest@localhost:5672",
            ["Auth:Authority"] = "http://localhost/test-realm",
            ["Auth:Audience"] = "uniplus",
        };

        // Hook para subclasses (ex.: OIDC real sobrescreve Auth:Authority/Audience).
        foreach (KeyValuePair<string, string?> over in OverridesAdicionais())
        {
            overrides[over.Key] = over.Value;
        }

        return overrides;
    }

    /// <summary>
    /// Overrides de configuração específicos da subclasse, aplicados após os
    /// defaults (vencem chaves duplicadas). Default: nenhum.
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() => [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICacheService>();
            services.AddScoped<ICacheService, FakeInMemoryCacheService>();
            services.AddCatalogoDePermissoes<PermissaoDeTeste>();

            // O registro de acesso real publica no Kafka, que as suítes não sobem; elas conferem o que a trilha recebeu.
            services.AdicionarRegistroDeDecisoesDeTeste();
        });
    }
}
