namespace Unifesspa.UniPlus.Infrastructure.Core.Smoke;

using Microsoft.Extensions.Logging;

/// <summary>
/// Consumer de <see cref="SmokePingMessage"/> — apenas registra log estruturado para
/// confirmação visual do round-trip pelo Wolverine outbox + transport (PG queue ou Kafka).
/// Discovery por convenção: a classe é estática e o método <c>Handle</c> é resolvido pelo
/// Wolverine na inicialização do host.
/// </summary>
public static partial class SmokePingHandler
{
    public static void Handle(SmokePingMessage message, ILogger<SmokePingMessage> logger)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(logger);

        LogSmokeReceived(logger, message.Id, message.Timestamp);
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "Smoke ping recebido pelo Wolverine: id={Id} timestamp={Timestamp:O}")]
    private static partial void LogSmokeReceived(ILogger logger, Guid id, DateTimeOffset timestamp);
}
