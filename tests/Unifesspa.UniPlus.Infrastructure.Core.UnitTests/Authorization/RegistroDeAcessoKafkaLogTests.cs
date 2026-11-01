namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

public sealed class RegistroDeAcessoKafkaLogTests
{
    [Theory(DisplayName = "Os níveis de log do librdkafka viram os níveis do ILogger, para o Serilog e o mascaramento os tratarem")]
    [InlineData(SyslogLevel.Emergency, LogLevel.Critical)]
    [InlineData(SyslogLevel.Critical, LogLevel.Critical)]
    [InlineData(SyslogLevel.Error, LogLevel.Error)]
    [InlineData(SyslogLevel.Warning, LogLevel.Warning)]
    [InlineData(SyslogLevel.Info, LogLevel.Information)]
    [InlineData(SyslogLevel.Debug, LogLevel.Debug)]
    public void ParaLogLevel_MapeiaONivel(SyslogLevel nivel, LogLevel esperado) =>
        RegistroDeAcessoKafka.ParaLogLevel(nivel).Should().Be(esperado);
}
