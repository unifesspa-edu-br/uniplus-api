namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System.Threading.Channels;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Repete, em segundo plano, o registro dos schemas cujo formato repete a falha transitória (JSON Schema), até o Schema
/// Registry confirmar. O schema Avro não passa por aqui: o serializador do Confluent o registra de novo em runtime.
/// </summary>
/// <remarks>
/// Cada schema tem a sua própria repetição, de modo que um que continue falhando não atrasa os demais. A espera dobra a
/// cada falha, até <see cref="EsperaMaxima"/>, e usa o <see cref="TimeProvider"/> do host (ADR-0068). A repetição para
/// ao confirmar, no encerramento do host, ou numa falha determinística, que fica registrada como erro e não é repetida.
/// Nunca bloqueia a subida nem o salvamento de quem depende do schema.
/// </remarks>
internal sealed partial class RepetidorDoRegistroDeSchema(
    RegistradorDeSchema registrador,
    TimeProvider relogio,
    ILogger<RepetidorDoRegistroDeSchema> logger) : BackgroundService
{
    internal static readonly TimeSpan EsperaInicial = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(5);

    private readonly Channel<SchemaRegistration> _pendentes = Channel.CreateUnbounded<SchemaRegistration>();

    public void Agendar(SchemaRegistration registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        _pendentes.Writer.TryWrite(registro);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> repeticoes = [];

        await IniciarRepeticoesAsync(repeticoes, stoppingToken).ConfigureAwait(false);
        await Task.WhenAll(repeticoes).ConfigureAwait(false);
    }

    private async Task IniciarRepeticoesAsync(List<Task> repeticoes, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (SchemaRegistration registro in _pendentes.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                repeticoes.Add(RepetirAteConfirmarAsync(registro, cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Encerramento do host: as repetições em curso também recebem o cancelamento.
        }
    }

    private async Task RepetirAteConfirmarAsync(SchemaRegistration registro, CancellationToken cancellationToken)
    {
        try
        {
            await RepetirAsync(registro, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Encerramento do host.
        }
        catch (Exception falha)
        {
            LogRepeticaoAbandonada(logger, registro.Subject, falha);
        }
    }

    private async Task RepetirAsync(SchemaRegistration registro, CancellationToken cancellationToken)
    {
        TimeSpan espera = EsperaInicial;

        do
        {
            await Task.Delay(espera, relogio, cancellationToken).ConfigureAwait(false);
            espera = Dobrar(espera);
        }
        while (await registrador.RegistrarAsync(registro, cancellationToken).ConfigureAwait(false) != ResultadoDoRegistroDeSchema.Confirmado);
    }

    private static TimeSpan Dobrar(TimeSpan espera) => TimeSpan.FromTicks(Math.Min(espera.Ticks * 2, EsperaMaxima.Ticks));

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A nova tentativa do registro do schema falhou de forma determinística e não será repetida. Subject={Subject}")]
    private static partial void LogRepeticaoAbandonada(ILogger logger, string subject, Exception exception);
}
