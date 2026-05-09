namespace Unifesspa.UniPlus.Infrastructure.Core.Smoke;

/// <summary>
/// Mensagem dummy publicada pelo endpoint smoke <c>/api/_smoke/messaging/publish</c>.
/// Serve como ping E2E do pipeline Wolverine + outbox + transport (PG queue ou Kafka).
/// O handler <see cref="SmokePingHandler"/> é registrado em <c>Infrastructure.Core</c>
/// e descoberto automaticamente pelo <c>UseWolverineOutboxCascading</c>.
/// </summary>
public sealed record SmokePingMessage(Guid Id, DateTimeOffset Timestamp);
