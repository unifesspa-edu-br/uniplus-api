namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization.RegistroDeAcessoDeTeste;

/// <summary>
/// O registro de acesso composto como a aplicação o compõe: o Schema Registry é obrigatório na configuração, mas a
/// aplicação sobe com ele fora do ar, e então a gravação falha como falha do broker, sem dar nada como gravado.
/// </summary>
public sealed class RegistroDeAcessoNaComposicaoTests
{
    // Porta onde nada escuta: a conexão é recusada na hora, como com o Schema Registry fora do ar.
    private const string SchemaRegistryForaDoAr = "http://127.0.0.1:9";

    [Fact(DisplayName = "Sem SchemaRegistry:Url a subida é recusada, porque o valor do registro é Avro")]
    public void SemUrlDoSchemaRegistry_RecusaASubida()
    {
        using ServiceProvider provedor = Compor(urlDoSchemaRegistry: null);

        Action acao = () => provedor.GetRequiredService<IRegistroOperacionalRestrito>();

        acao.Should().Throw<InvalidOperationException>().WithMessage("*SchemaRegistry:Url*");
    }

    [Fact(DisplayName = "Com o Schema Registry configurado o esquema do registro de acesso é registrado na subida")]
    public void ComUrlDoSchemaRegistry_RegistraOEsquemaNaSubida()
    {
        using ServiceProvider provedor = Compor(SchemaRegistryForaDoAr);

        provedor.GetServices<SchemaRegistration>().Should().ContainSingle(registro =>
            registro.Subject == "autorizacao_registros_de_acesso-value"
            && registro.SchemaResourceName == EsquemaDoRegistroDeAcesso.NomeDoRecurso);
    }

    [Fact(DisplayName = "O subject registrado na subida acompanha o tópico configurado, que é o que o serializador usa")]
    public void TopicoConfigurado_DefineOSubjectDaSubida()
    {
        ServiceCollection servicos = Servicos(SchemaRegistryForaDoAr, topico: "outro_topico");
        using ServiceProvider provedor = servicos.BuildServiceProvider();

        provedor.GetServices<SchemaRegistration>().Should().ContainSingle().Which.Subject.Should().Be("outro_topico-value");
    }

    [Fact(DisplayName = "A aplicação sobe com o Schema Registry fora do ar")]
    public async Task SchemaRegistryForaDoAr_ASubidaNaoFalha()
    {
        using ServiceProvider provedor = Compor(SchemaRegistryForaDoAr);

        foreach (IHostedService servico in provedor.GetServices<IHostedService>())
        {
            await servico.StartAsync(default);
        }

        provedor.GetRequiredService<IRegistroOperacionalRestrito>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Com o Schema Registry fora do ar, o primeiro registro falha como falha do broker, e nada é dado como gravado")]
    public async Task SchemaRegistryForaDoAr_AGravacaoFalhaComoOBroker()
    {
        using ServiceProvider provedor = Compor(SchemaRegistryForaDoAr);
        IRegistroOperacionalRestrito registro = provedor.GetRequiredService<IRegistroOperacionalRestrito>();

        Func<Task> acao = () => registro.RegistrarAsync([Registro()], default);

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    [Fact(DisplayName = "O cliente do Schema Registry é um só, o que o módulo da Seleção registra antes da construção do container")]
    public void ClienteDoSchemaRegistry_EUmSo()
    {
        ServiceCollection servicos = Servicos(SchemaRegistryForaDoAr);
        using CachedSchemaRegistryClient cliente = new(new SchemaRegistryConfig { Url = SchemaRegistryForaDoAr });
        servicos.AddSingleton<ISchemaRegistryClient>(cliente);
        using ServiceProvider provedor = servicos.BuildServiceProvider();

        provedor.GetRequiredService<ISchemaRegistryClient>().Should().BeSameAs(cliente);
        provedor.GetRequiredService<IRegistroOperacionalRestrito>().Should().NotBeNull();
    }

    private static ServiceProvider Compor(string? urlDoSchemaRegistry) => Servicos(urlDoSchemaRegistry).BuildServiceProvider();

    private static ServiceCollection Servicos(string? urlDoSchemaRegistry, string? topico = null)
    {
        ServiceCollection servicos = [];
        servicos.AddLogging(static builder => builder.SetMinimumLevel(LogLevel.None));
        servicos.AddSingleton(TimeProvider.System);
        servicos.AddAutorizacao(new ConfigurationBuilder().AddInMemoryCollection(Configuracao(urlDoSchemaRegistry, topico)).Build());

        return servicos;
    }

    private static Dictionary<string, string?> Configuracao(string? urlDoSchemaRegistry, string? topico)
    {
        Dictionary<string, string?> configuracao = new()
        {
            ["RegistroDeAcesso:Kafka:BootstrapServers"] = "127.0.0.1:9",
            ["RegistroDeAcesso:Kafka:PrazoDeConfirmacao"] = "00:00:02",
            ["SchemaRegistry:Url"] = urlDoSchemaRegistry,
            ["SchemaRegistry:RequestTimeoutMs"] = "2000",
        };
        if (topico is not null)
        {
            configuracao["RegistroDeAcesso:Kafka:Topico"] = topico;
        }

        return configuracao;
    }
}
