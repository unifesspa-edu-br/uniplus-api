namespace Unifesspa.UniPlus.Verificador.Configuracao;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

/// <summary>Filas, paralelismo e política de erro do verificador (ADR-0146).</summary>
public static class ConfiguracaoDoWolverine
{
    /// <summary>As esperas das três novas tentativas quando o clamd ou o armazenamento não respondem.</summary>
    public static readonly TimeSpan[] EsperasEntreTentativas =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)];

    public static void Configurar(WolverineOptions opcoes, VerificadorOptions verificador)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(verificador);

        RabbitMqOptions rabbit = verificador.RabbitMq;
        opcoes.UseRabbitMq(fabrica =>
        {
            if (!string.IsNullOrWhiteSpace(rabbit.Uri))
            {
                fabrica.Uri = new Uri(rabbit.Uri);
                return;
            }

            fabrica.HostName = rabbit.Host;
            fabrica.Port = rabbit.Port;
            fabrica.VirtualHost = rabbit.VirtualHost;
            fabrica.UserName = rabbit.Usuario;
            fabrica.Password = rabbit.Senha;
        })
        .AutoProvision();

        opcoes.ListenToRabbitQueue(FilasDaVerificacao.Pedidos, fila => fila.QueueType = QueueType.quorum)
            .ProcessInline()
            .ListenerCount(verificador.Paralelismo)
            .PreFetchCount(1);

        // No modo inline, a concorrência vem de um listener por slot de paralelismo, cada um com um pedido por vez.
        // Inline: o pedido só é confirmado depois de o RabbitMQ confirmar a publicação do resultado.
        opcoes.PublishMessage<ResultadoDaVerificacaoDeArquivo>()
            .ToRabbitQueue(FilasDaVerificacao.Resultados, fila => fila.QueueType = QueueType.quorum)
            .SendInline();

        // O erro permanente do clamd não passa por aqui: o verificador o converte em recusa.
        opcoes.OnException<ClamdIndisponivelException>()
            .Or<ArmazenamentoException>()
            .RetryWithCooldown(EsperasEntreTentativas)
            .Then.MoveToErrorQueue();
    }
}
