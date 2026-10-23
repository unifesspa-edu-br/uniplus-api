namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.Postgresql.Transport;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

/// <summary>
/// O transporte entre a confirmação na API e o verificador, escolhido por <c>TRANSPORTE</c>:
/// <list type="bullet">
/// <item><c>rabbitmq</c>: quorum queues; o verificador não acessa o banco da API.</item>
/// <item><c>postgres</c>: a fila do próprio Wolverine no Postgres, a mesma das filas internas da
/// API. Cada papel guarda o seu inbox/outbox num schema próprio; só as tabelas das filas são
/// comuns (<c>wolverine_filas</c>).</item>
/// </list>
/// Nos dois casos, a API grava o arquivo como "em verificação" e o pedido de verificação no outbox,
/// na mesma transação: não há arquivo em verificação sem pedido, nem pedido sem arquivo.
/// </summary>
public static class Fila
{
    public const string Verificacao = "verificacao";
    public const string Resultados = "resultado_verificacao";
    private const string SchemaDasFilas = "wolverine_filas";

    public static bool Postgres => string.Equals(Configuracao.Obrigatoria("TRANSPORTE"), "postgres", StringComparison.Ordinal);

    public static void ConfigurarApi(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string banco = Configuracao.Obrigatoria("POSTGRES");
        builder.Services.AddDbContextWithWolverineIntegration<Banco>(o => o.UseNpgsql(banco), "wolverine_api");
        builder.Host.UseWolverine(opts =>
        {
            opts.ServiceName = "api";
            opts.Discovery.DisableConventionalDiscovery().IncludeType<ResultadoDaVerificacaoHandler>();
            Persistir(opts, banco, "wolverine_api");
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.AutoApplyTransactions();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
            opts.Policies.UseDurableInboxOnAllListeners();

            if (Postgres)
            {
                opts.PublishMessage<VerificarArquivo>().ToPostgresqlQueue(Verificacao);
                Polling(opts.ListenToPostgresqlQueue(Resultados));
            }
            else
            {
                opts.UseRabbitMq(new Uri(Configuracao.Obrigatoria("RABBITMQ"))).AutoProvision();
                opts.PublishMessage<VerificarArquivo>().ToRabbitQueue(Verificacao, q => q.QueueType = QueueType.quorum);
                opts.ListenToRabbitQueue(Resultados, q => q.QueueType = QueueType.quorum);
            }
        });
    }

    public static void ConfigurarVerificador(WebApplicationBuilder builder, Configuracao config)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(config);

        int paralelismo = int.Parse(Configuracao.Obrigatoria("PARALELISMO"), CultureInfo.InvariantCulture);
        builder.Services.AddSingleton(config.ClienteInterno());
        builder.Services.AddSingleton(new Clamd(config.ClamdHost, config.ClamdPorta));
        builder.Host.UseWolverine(opts =>
        {
            opts.ServiceName = "verificador";
            opts.Discovery.DisableConventionalDiscovery().IncludeType<VerificadorDaFila>();

            // O clamd fora do ar é passageiro: três novas tentativas espaçadas e, depois, a fila de
            // erro. O arquivo continua "em verificação" e a reconciliação da API o republica.
            opts.Policies.OnException<ClamdIndisponivelException>()
                .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
                .Then.MoveToErrorQueue();
            opts.Policies.OnAnyException()
                .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
                .Then.MoveToErrorQueue();

            if (Postgres)
            {
                Persistir(opts, Configuracao.Obrigatoria("POSTGRES"), "wolverine_verificador");
                opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
                opts.Policies.UseDurableInboxOnAllListeners();
                Polling(opts.ListenToPostgresqlQueue(Verificacao)
                    .MaximumMessagesToReceive(paralelismo)
                    .MaximumParallelMessages(paralelismo));
                opts.PublishMessage<ArquivoVerificado>().ToPostgresqlQueue(Resultados);
            }
            else
            {
                // Sem banco: cada consumidor confirma a mensagem ao RabbitMQ só depois de processá-la
                // e de entregar o veredicto. Se o verificador cair no meio, a mensagem volta à fila.
                opts.UseRabbitMq(new Uri(Configuracao.Obrigatoria("RABBITMQ"))).AutoProvision();
                opts.ListenToRabbitQueue(Verificacao, q => q.QueueType = QueueType.quorum)
                    .ProcessInline()
                    .ListenerCount(paralelismo)
                    .PreFetchCount(1);
                opts.PublishMessage<ArquivoVerificado>()
                    .ToRabbitQueue(Resultados, q => q.QueueType = QueueType.quorum)
                    .SendInline();
            }
        });
    }

    private static void Persistir(WolverineOptions opts, string banco, string schema)
    {
        IPostgresqlBackedPersistence persistencia = opts.PersistMessagesWithPostgresql(banco, schema);
        if (Postgres)
        {
            persistencia.EnableMessageTransport(_ => { });
            opts.Transports.GetOrCreate<PostgresqlTransport>().TransportSchemaName = SchemaDasFilas;
        }
    }

    /// <summary>A fila do Postgres é lida por polling; sem <c>POLLING_MS</c>, vale o padrão do Wolverine.</summary>
    private static void Polling(PostgresqlListenerConfiguration ouvinte)
    {
        if (Environment.GetEnvironmentVariable("POLLING_MS") is { Length: > 0 } ms)
        {
            ouvinte.PollingInterval(TimeSpan.FromMilliseconds(int.Parse(ms, CultureInfo.InvariantCulture)));
        }
    }
}
