namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.API;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using Wolverine;
using Wolverine.Configuration;
using Wolverine.Postgresql.Transport;
using Wolverine.Runtime;

/// <summary>
/// O roteamento da mensagem que remove os objetos do armazenamento de um rascunho excluído: ela vai para a fila
/// PostgreSQL própria, que tem listener, e sai agendada para depois da URL de envio mais a tolerância dos pendentes vencidos.
/// </summary>
public sealed class RoteamentoDaRemocaoDosArquivosDoRascunhoTests
{
    // O transporte PostgreSQL grava o nome da fila com sublinhado no lugar do hífen.
    private const string NomeDaFilaNoTransporte = "selecao_remocao_arquivos_rascunho";

    private static Endpoint EndpointDaFila()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RabbitMq:Uri"] = "amqp://localhost:5672" })
            .Build();
        WolverineOptions opts = new();
        SelecaoMessagingRegistration.ConfigurarRouting(opts, configuracao, srClient: null);

        Endpoint fila = opts.Transports.GetOrCreate<PostgresqlTransport>().Endpoints().Single(e => e.Uri.Host == NomeDaFilaNoTransporte);

        return Compilado(fila, opts);
    }

    // A configuração da fila é aplicada na compilação do endpoint, na subida do Wolverine; sem compilar, o teste leria o
    // padrão e não o efetivo.
    private static Endpoint Compilado(Endpoint endpoint, WolverineOptions opts)
    {
        IWolverineRuntime runtime = Substitute.For<IWolverineRuntime>();
        runtime.Options.Returns(opts);
        endpoint.Compile(runtime);

        return endpoint;
    }

    [Fact(DisplayName = "A fila da remoção dos objetos do rascunho excluído tem listener")]
    public void FilaDaRemocao_TemListener() => EndpointDaFila().IsListener.Should().BeTrue();

    [Fact(DisplayName = "A mensagem sai agendada para depois da URL de envio mais a tolerância dos pendentes vencidos")]
    public void MensagemDaRemocao_SaiAgendadaParaDepoisDoPrazoDaUrlDeEnvio()
    {
        Envelope envelope = new(new RemoverArquivosDoRascunhoExcluido(["chave"]));
        DateTimeOffset antes = DateTimeOffset.UtcNow;

        foreach (IEnvelopeRule regra in EndpointDaFila().OutgoingRules)
        {
            regra.Modify(envelope);
        }

        envelope.ScheduledTime.Should().BeOnOrAfter(antes + RemoverArquivosDoRascunhoExcluido.Atraso)
            .And.BeOnOrBefore(DateTimeOffset.UtcNow + RemoverArquivosDoRascunhoExcluido.Atraso);
    }
}
