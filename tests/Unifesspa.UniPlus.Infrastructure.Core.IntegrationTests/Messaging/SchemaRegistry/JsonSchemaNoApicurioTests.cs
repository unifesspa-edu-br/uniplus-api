namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Messaging.SchemaRegistry;

using System.Net;
using System.Text;

using AwesomeAssertions;

using Confluent.SchemaRegistry;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>
/// O registro de JSON Schema contra o Apicurio 3.2.4 real: o dialeto 2020-12 registra várias versões sob a regra
/// <c>NONE</c> do subject, o mesmo conteúdo devolve o mesmo id, e a regra é conferida pela regra <b>efetiva</b>: um
/// subject sem regra própria, sob uma regra global diferente, é recusado, e a leitura simples (que devolve um falso
/// <c>NONE</c> nesse caso) não o deixa passar.
/// </summary>
public sealed class JsonSchemaNoApicurioTests : IAsyncLifetime
{
    private const string Imagem = "apicurio/apicurio-registry:3.2.4";
    private const int PortaInterna = 8081;
    private const string Recursos = "Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Messaging.SchemaRegistry.Recursos.";

    private static readonly HttpClient Http = new();

    private IContainer? _schemaRegistry;
    private string _url = string.Empty;

    public async Task InitializeAsync()
    {
        _schemaRegistry = new ContainerBuilder(Imagem)
            .WithPortBinding(PortaInterna, assignRandomHostPort: true)
            .WithEnvironment("QUARKUS_HTTP_PORT", PortaInterna.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithEnvironment("APICURIO_STORAGE_KIND", "sql")
            .WithEnvironment("APICURIO_STORAGE_SQL_KIND", "h2")
            .WithEnvironment("APICURIO_AUTH_ENABLED", "false")
            .WithEnvironment("QUARKUS_OIDC_ENABLED", "false")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                requisicao => requisicao.ForPort(PortaInterna).ForPath("/apis/registry/v3/system/info")))
            .Build();
        await _schemaRegistry.StartAsync();
        _url = $"http://localhost:{_schemaRegistry.GetMappedPublicPort(PortaInterna)}/apis/ccompat/v7";
    }

    public async Task DisposeAsync()
    {
        if (_schemaRegistry is not null)
        {
            await _schemaRegistry.DisposeAsync();
        }
    }

    [Fact(DisplayName = "Com a regra NONE própria no subject, o 2020-12 registra três versões, mesmo com a global mais restrita")]
    public async Task RegraPropriaNone_RegistraAsVersoes()
    {
        const string subject = "documento-regra-propria-v1";
        await DefinirRegraAsync(null, "FULL");
        await DefinirRegraAsync(subject, "NONE");
        using ServiceProvider provedor = Compor(subject, "DocumentoV1", "DocumentoV2", "DocumentoV3");

        await IniciarAsync(provedor);

        (await provedor.GetRequiredService<ISchemaRegistryClient>().GetSubjectVersionsAsync(subject)).Should().HaveCount(3);
    }

    [Fact(DisplayName = "O mesmo conteúdo registrado de novo devolve o mesmo id e não cria versão")]
    public async Task MesmoConteudo_MesmoId()
    {
        const string subject = "documento-idempotente-v1";
        await DefinirRegraAsync(null, "NONE");
        using ServiceProvider provedor = Compor(subject, "DocumentoV1");
        await IniciarAsync(provedor);
        ISchemaRegistryClient cliente = provedor.GetRequiredService<ISchemaRegistryClient>();
        int primeiro = (await cliente.GetLatestSchemaAsync(subject)).Id;

        using ServiceProvider outroHost = Compor(subject, "DocumentoV1");
        await IniciarAsync(outroHost);

        (await cliente.GetLatestSchemaAsync(subject)).Id.Should().Be(primeiro);
        (await cliente.GetSubjectVersionsAsync(subject)).Should().HaveCount(1);
    }

    [Fact(DisplayName = "Sem regra própria e com a global diferente de NONE, a subida é recusada: a regra efetiva não é NONE")]
    public async Task SemRegraPropriaEGlobalDiferente_RecusaASubida()
    {
        const string subject = "documento-global-diferente-v1";
        await DefinirRegraAsync(null, "FULL");
        using ServiceProvider provedor = Compor(subject, "DocumentoV1");

        Func<Task> acao = () => IniciarAsync(provedor);

        (await acao.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(subject).And.Contain("Full");
        (await provedor.GetRequiredService<ISchemaRegistryClient>().GetAllSubjectsAsync()).Should().NotContain(subject);
    }

    [Fact(DisplayName = "A leitura simples do cliente dá um falso NONE para subject sem regra própria, e o leitor não cai nele")]
    public async Task LeituraSimples_DaFalsoNone_ELeitorNaoCai()
    {
        const string subject = "documento-falso-none-v1";
        await DefinirRegraAsync(null, "FULL");
        using ServiceProvider provedor = Compor(subject, "DocumentoV1");
        await RegistrarSemConferirAsync(provedor, subject);

        Compatibility lidaPeloCliente = await provedor.GetRequiredService<ISchemaRegistryClient>().GetCompatibilityAsync(subject);
        Compatibility efetiva = await provedor.GetRequiredService<ILeitorDaRegraDeCompatibilidadeDoSubject>().LerRegraDeCompatibilidadeEfetivaAsync(subject, default);

        lidaPeloCliente.Should().Be(Compatibility.None, "o Apicurio devolve NONE para o subject sem regra própria");
        efetiva.Should().Be(Compatibility.Full, "mas aplica a global, e o leitor pede a regra efetiva");
    }

    [Fact(DisplayName = "Sem regra própria e com a global NONE, a regra efetiva é NONE e a subida registra")]
    public async Task SemRegraPropriaEGlobalNone_Registra()
    {
        const string subject = "documento-global-none-v1";
        await DefinirRegraAsync(null, "NONE");
        using ServiceProvider provedor = Compor(subject, "DocumentoV1");

        await IniciarAsync(provedor);

        (await provedor.GetRequiredService<ISchemaRegistryClient>().GetSubjectVersionsAsync(subject)).Should().HaveCount(1);
    }

    private ServiceProvider Compor(string subject, params string[] versoes)
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SchemaRegistry:Url"] = _url })
            .Build();
        ServiceCollection servicos = [];
        servicos.AddLogging();
        servicos.AddSingleton(TimeProvider.System);
        SchemaRegistryBuilder builder = servicos.AddSchemaRegistry(configuracao);
        foreach (string versao in versoes)
        {
            builder.AddJsonSchema(subject, $"{Recursos}{versao}.schema.json", typeof(JsonSchemaNoApicurioTests).Assembly);
        }

        return servicos.BuildServiceProvider();
    }

    private static async Task IniciarAsync(ServiceProvider provedor)
    {
        foreach (IHostedService servico in provedor.GetServices<IHostedService>().OfType<SchemaRegistrationHostedService>())
        {
            await servico.StartAsync(default);
        }
    }

    // Registra pelo cliente, sem a conferência da regra, para criar o subject sem regra própria.
    private static async Task RegistrarSemConferirAsync(ServiceProvider provedor, string subject)
    {
        using Stream recurso = typeof(JsonSchemaNoApicurioTests).Assembly.GetManifestResourceStream($"{Recursos}DocumentoV1.schema.json")!;
        using StreamReader leitor = new(recurso);
        await provedor.GetRequiredService<ISchemaRegistryClient>()
            .RegisterSchemaAsync(subject, new Schema(await leitor.ReadToEndAsync(), SchemaType.Json));
    }

    private async Task DefinirRegraAsync(string? subject, string nivel)
    {
        using StringContent corpo = new($$"""{"compatibility":"{{nivel}}"}""", Encoding.UTF8, "application/vnd.schemaregistry.v1+json");
        string caminho = subject is null ? "config" : $"config/{Uri.EscapeDataString(subject)}";
        using HttpResponseMessage resposta = await Http.PutAsync(new Uri($"{_url}/{caminho}"), corpo);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
