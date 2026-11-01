namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Authorization;

using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using AwesomeAssertions;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O registro de acesso publicado no tópico próprio, contra um Kafka real: o lote sai com as chaves do registro e sem o
/// valor revelado, e sem broker a gravação falha em vez de passar.
/// </summary>
public sealed class RegistroDeAcessoKafkaTests : IAsyncLifetime
{
    private const string Imagem = "apache/kafka:4.2.0";
    private const int PortaInterna = 9092;
    private const string TopicoDoTeste = "topico_do_lote";

    private IContainer? _kafka;
    private string _bootstrap = string.Empty;

    public async Task InitializeAsync()
    {
        int porta = PortaLivre();
        _bootstrap = $"localhost:{porta}";
        _kafka = new ContainerBuilder(Imagem)
            .WithPortBinding(porta, PortaInterna)
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_LISTENERS", $"PLAINTEXT://0.0.0.0:29092,CONTROLLER://0.0.0.0:9093,PLAINTEXT_HOST://0.0.0.0:{PortaInterna}")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://localhost:29092,PLAINTEXT_HOST://{_bootstrap}")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:9093")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Kafka Server started"))
            .Build();
        await _kafka.StartAsync();

        // O produtor não cria tópico: a fixture o cria, como o compose faz em dev.
        await CriarTopicoAsync(TopicoDoTeste);
    }

    private async Task<string> CriarTopicoAsync(string nome)
    {
        using IAdminClient admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _bootstrap }).Build();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = nome, NumPartitions = 1, ReplicationFactor = 1 }]);
        return nome;
    }

    public async Task DisposeAsync()
    {
        if (_kafka is not null)
        {
            await _kafka.DisposeAsync();
        }
    }

    [Fact(DisplayName = "O lote é uma única mensagem no tópico, com a chave de cada registro, e sem o valor revelado")]
    public async Task Lote_PublicaUmaMensagemUnicaComTodasAsChaves()
    {
        Guid fernanda = Guid.CreateVersion7();
        Guid caroline = Guid.CreateVersion7();
        IReadOnlyList<RegistroDecisaoAcesso> lote = Registro()
            .ParaCadaResource("inscricao", [fernanda, caroline], resourceEhInscricao: true);
        using RegistroDeAcessoKafka registro = Criar(_bootstrap, TimeSpan.FromSeconds(20), topico: TopicoDoTeste);

        await registro.RegistrarAsync(lote, default);

        ConsumeResult<string, string> mensagem = Consumir(TopicoDoTeste).Should().ContainSingle().Subject;
        using JsonDocument json = JsonDocument.Parse(mensagem.Message.Value);
        json.RootElement.EnumerateArray().Select(static r => r.GetProperty("chave").GetString())
            .Should().BeEquivalentTo(lote.Select(static r => r.Chave));
        json.RootElement.EnumerateArray().Should().OnlyContain(e =>
            e.GetProperty("registro").GetProperty("atorSubject").GetString() == "leandro.viana"
            && e.GetProperty("registro").GetProperty("inscricaoId").GetGuid() == e.GetProperty("registro").GetProperty("resourceId").GetGuid());
    }

    [Fact(DisplayName = "O registro da revelação leva o código do fato, e nunca o valor")]
    public async Task Revelacao_LevaOCodigoDoFato()
    {
        string topico = await CriarTopicoAsync("revelacao");
        using RegistroDeAcessoKafka registro = Criar(_bootstrap, TimeSpan.FromSeconds(20), topico: topico);

        await registro.RegistrarAsync([Registro(fatoCodigo: "CPF")], default);

        using JsonDocument json = JsonDocument.Parse(Consumir(topico).Should().ContainSingle().Subject.Message.Value);
        json.RootElement[0].GetProperty("registro").GetProperty("fatoCodigo").GetString().Should().Be("CPF");
    }

    [Theory(DisplayName = "A mensagem publicada traz o client OIDC em clienteOidc, ou nulo quando o token não o traz")]
    [InlineData("selecao-web")]
    [InlineData(null)]
    public async Task Registro_PublicaOClienteOidc(string? azp)
    {
        string topico = await CriarTopicoAsync($"cliente-{Guid.NewGuid():N}");
        using RegistroDeAcessoKafka registro = Criar(_bootstrap, TimeSpan.FromSeconds(20), topico: topico);

        await registro.RegistrarAsync([Registro(clienteOidc: azp)], default);

        using JsonDocument json = JsonDocument.Parse(Consumir(topico).Should().ContainSingle().Subject.Message.Value);
        JsonElement cliente = json.RootElement[0].GetProperty("registro").GetProperty("clienteOidc");
        (cliente.ValueKind == JsonValueKind.Null ? null : cliente.GetString()).Should().Be(azp);
    }

    [Fact(DisplayName = "Sem broker disponível, a gravação falha com registro não gravado")]
    public async Task SemBroker_FalhaComRegistroNaoGravado()
    {
        using RegistroDeAcessoKafka registro = Criar("localhost:1", TimeSpan.FromSeconds(3));

        Func<Task> acao = () => registro.RegistrarAsync([Registro()], default);

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    [Fact(DisplayName = "O produtor não cria o tópico: sem ele, a gravação falha")]
    public async Task TopicoInexistente_FalhaComRegistroNaoGravado()
    {
        using RegistroDeAcessoKafka registro = Criar(_bootstrap, TimeSpan.FromSeconds(4), topico: "topico_que_nao_existe");

        Func<Task> acao = () => registro.RegistrarAsync([Registro()], default);

        await acao.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    [Theory(DisplayName = "Sem bootstrap servers em RegistroDeAcesso:Kafka, o registro não é criado e a mensagem nomeia a chave")]
    [InlineData("")]
    [InlineData("   ")]
    public void SemKafkaConfigurado_NaoCria(string bootstrap)
    {
        Action acao = () => _ = Criar(bootstrap, TimeSpan.FromSeconds(3));

        acao.Should().Throw<OptionsValidationException>().WithMessage("*RegistroDeAcesso:Kafka:BootstrapServers*");
    }

    [Theory(DisplayName = "Prazo de confirmação abaixo de 100 ms é recusado: zero é prazo infinito no cliente e o prazo precisa superar o agrupamento do produtor")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(50)]
    public void PrazoForaDaFaixa_NaoCria(int milissegundos)
    {
        Action acao = () => _ = Criar(_bootstrap, TimeSpan.FromMilliseconds(milissegundos));

        acao.Should().Throw<OptionsValidationException>().WithMessage("*PrazoDeConfirmacao*");
    }

    [Fact(DisplayName = "SASL exige mecanismo, e SSL exige a CA")]
    public void SaslSemMecanismo_NaoCria()
    {
        Action acao = () => _ = Criar("kafka:9093", TimeSpan.FromSeconds(3), protocolo: "SASL_SSL");

        acao.Should().Throw<OptionsValidationException>().WithMessage("*SaslMechanism*").WithMessage("*CaminhoDoCertificadoCa*");
    }

    [Fact(DisplayName = "SASL com mecanismo exige usuário e senha")]
    public void SaslSemCredencial_NaoCria()
    {
        Action acao = () => _ = Criar("kafka:9093", TimeSpan.FromSeconds(3), protocolo: "SASL_PLAINTEXT", mecanismo: "SCRAM-SHA-512");

        acao.Should().Throw<OptionsValidationException>().WithMessage("*Usuario*").WithMessage("*Senha*");
    }

    // Resolve pelo contêiner, como a aplicação: é a leitura das opções que valida a configuração.
    private static RegistroDeAcessoKafka Criar(string bootstrap, TimeSpan prazo, string? topico = null, string? protocolo = null, string? mecanismo = null)
    {
        Dictionary<string, string?> configuracao = new()
        {
            ["RegistroDeAcesso:Kafka:BootstrapServers"] = bootstrap,
            ["RegistroDeAcesso:Kafka:PrazoDeConfirmacao"] = prazo.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
            ["RegistroDeAcesso:Kafka:Topico"] = topico ?? RegistroDeAcessoKafkaOptions.TopicoPadrao,
            ["RegistroDeAcesso:Kafka:SecurityProtocol"] = protocolo,
            ["RegistroDeAcesso:Kafka:SaslMechanism"] = mecanismo,
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configuracao).Build();
        ServiceCollection services = new();
        services.AddAutorizacao(config);
        ServiceProvider provedor = services.BuildServiceProvider();

        return (RegistroDeAcessoKafka)provedor.GetRequiredService<IRegistroOperacionalRestrito>();
    }

    private List<ConsumeResult<string, string>> Consumir(string topico)
    {
        ConsumerConfig config = new()
        {
            BootstrapServers = _bootstrap,
            GroupId = $"teste-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };
        using IConsumer<string, string> consumidor = new ConsumerBuilder<string, string>(config).Build();
        consumidor.Subscribe(topico);

        List<ConsumeResult<string, string>> recebidas = [];
        DateTime limite = DateTime.UtcNow.AddSeconds(30);

        while (recebidas.Count == 0 && DateTime.UtcNow < limite)
        {
            if (consumidor.Consume(TimeSpan.FromSeconds(1)) is { } mensagem)
            {
                recebidas.Add(mensagem);
            }
        }

        // Janela para qualquer outra mensagem que viesse junto: o lote é uma só.
        while (consumidor.Consume(TimeSpan.FromSeconds(2)) is { } extra)
        {
            recebidas.Add(extra);
        }

        return recebidas;
    }

    private static RegistroDecisaoAcesso Registro(string? fatoCodigo = null, string? clienteOidc = null)
    {
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "leandro.viana").Value!, "jti-teste").Value!;
        EffectiveGrant concessao = EffectiveGrant.From("selecao:inscricoes:revelar-cpf", FonteGrant.Concessao, escopoInstitucional: true).Value!;

        return RegistroDecisaoAcesso.De(
            sujeito,
            PermissionRequirement.From("selecao:inscricoes:revelar-cpf", ClassificacaoDeProtecao.Identificador, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!,
            ResourceContext.From("inscricao", ClassificacaoDeProtecao.Identificador, fatoCodigo: fatoCodigo).Value!,
            AuthorizationRequestContext.From($"req-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, OrigemRequisicao.Api, clienteOidc: clienteOidc).Value!,
            AuthorizationDecision.Permitir(concessao));
    }

    private static int PortaLivre()
    {
        using TcpListener escuta = new(IPAddress.Loopback, 0);
        escuta.Start();
        return ((IPEndPoint)escuta.LocalEndpoint).Port;
    }
}
