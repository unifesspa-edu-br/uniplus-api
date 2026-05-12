namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Collections.Generic;

using AwesomeAssertions;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using EditalPublicadoAvro = unifesspa.uniplus.selecao.events.EditalPublicado;

/// <summary>
/// Unit tests do logging estruturado do <see cref="EditalPublicadoToKafkaCascadeHandler"/>
/// (story uniplus-api#427). Cobre:
///   - <c>Handle()</c> projeta o Avro mapeado corretamente (mantém invariante existente).
///   - <c>Handle()</c> emite log Information com <c>EventId</c>/<c>EditalId</c>/<c>NumeroEdital</c>
///     como propriedades estruturadas (consultáveis via LogQL no Loki, não só texto).
///   - <c>Handle()</c> rejeita <c>@event</c> e <c>logger</c> nulos com
///     <see cref="ArgumentNullException"/>.
/// </summary>
[Trait("Category", "OutboxCascadingUnit")]
public sealed class EditalPublicadoToKafkaCascadeHandlerLoggingTests
{
    [Fact(DisplayName = "Handle projeta EditalPublicadoEvent para EditalPublicadoAvro preservando EditalId e NumeroEdital")]
    public void Handle_RetornaAvroMapeado()
    {
        Guid editalId = Guid.CreateVersion7();
        const string numeroEdital = "42/2026";
        EditalPublicadoEvent @event = new(editalId, numeroEdital);
        EstruturalLogger<EditalPublicadoToKafkaCascadeHandler> logger = new();

        EditalPublicadoAvro avro = EditalPublicadoToKafkaCascadeHandler.Handle(@event, logger);

        // Acerto de mapping permanece a invariante; logging entrou junto sem
        // mudar o contrato cross-module.
        avro.EditalId.Should().Be(editalId.ToString());
        avro.NumeroEdital.Should().Be(numeroEdital);
    }

    [Fact(DisplayName = "Handle emite log Information com EventId/EditalId/NumeroEdital como properties estruturadas")]
    public void Handle_EmiteLogStruturadoAntesDaProjecao()
    {
        Guid editalId = Guid.CreateVersion7();
        const string numeroEdital = "99/2030";
        EditalPublicadoEvent @event = new(editalId, numeroEdital);
        EstruturalLogger<EditalPublicadoToKafkaCascadeHandler> logger = new();

        _ = EditalPublicadoToKafkaCascadeHandler.Handle(@event, logger);

        logger.Entradas.Should().ContainSingle(
            "exatamente uma chamada ao LoggerMessage por execução do Handle");
        EstruturalLogger<EditalPublicadoToKafkaCascadeHandler>.Entrada entrada = logger.Entradas[0];
        entrada.Level.Should().Be(LogLevel.Information);
        // Property name match do template `Projetando ... EventId={EventId} EditalId={EditalId} NumeroEdital={NumeroEdital}`.
        // O [LoggerMessage] source generator emite cada placeholder como key
        // estruturado — Loki/Grafana indexa diretamente.
        entrada.Properties.Should().ContainKey("EventId");
        entrada.Properties.Should().ContainKey("EditalId");
        entrada.Properties.Should().ContainKey("NumeroEdital");
        entrada.Properties["EventId"].Should().Be(@event.EventId);
        entrada.Properties["EditalId"].Should().Be(editalId);
        entrada.Properties["NumeroEdital"].Should().Be(numeroEdital);
    }

    [Fact(DisplayName = "Handle lança ArgumentNullException com event nulo")]
    public void Handle_ComEventNulo_LancaArgumentNullException()
    {
        EstruturalLogger<EditalPublicadoToKafkaCascadeHandler> logger = new();

        Action act = () => EditalPublicadoToKafkaCascadeHandler.Handle(null!, logger);

        // ArgumentNullException.ThrowIfNull captura o nome do argumento C#
        // via CallerArgumentExpression — como o parâmetro do handler é
        // `@event` (palavra-chave escapada), o ParamName preserva o '@'.
        act.Should().Throw<ArgumentNullException>().WithParameterName("@event");
        logger.Entradas.Should().BeEmpty("guard de argumento precede a emissão do log");
    }

    [Fact(DisplayName = "Handle lança ArgumentNullException com logger nulo")]
    public void Handle_ComLoggerNulo_LancaArgumentNullException()
    {
        EditalPublicadoEvent @event = new(Guid.CreateVersion7(), "1/2026");

        Action act = () => EditalPublicadoToKafkaCascadeHandler.Handle(@event, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    /// <summary>
    /// Captura mensagem + level + properties estruturadas do <c>ILogger</c>.
    /// Diferente de um FakeLogger que só guarda a mensagem formatada, este
    /// preserva o <see cref="IReadOnlyList{KeyValuePair}"/> com as
    /// propriedades nomeadas que o <c>[LoggerMessage]</c> source generator
    /// produz — essencial para validar que Loki/Grafana indexa cada campo
    /// como label estruturado, não como texto livre.
    /// </summary>
    internal sealed class EstruturalLogger<T> : ILogger<T>
    {
        public List<Entrada> Entradas { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            Dictionary<string, object?> properties = new(StringComparer.Ordinal);
            if (state is IReadOnlyList<KeyValuePair<string, object?>> kvList)
            {
                foreach (KeyValuePair<string, object?> kv in kvList)
                {
                    properties[kv.Key] = kv.Value;
                }
            }

            Entradas.Add(new Entrada(logLevel, formatter(state, exception), properties));
        }

        internal sealed record Entrada(
            LogLevel Level,
            string Message,
            IReadOnlyDictionary<string, object?> Properties);
    }
}
