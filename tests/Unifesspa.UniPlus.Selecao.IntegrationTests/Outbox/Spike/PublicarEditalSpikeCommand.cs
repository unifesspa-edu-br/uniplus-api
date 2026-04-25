namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Spike;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// SPIKE V3 — comando enviado via Wolverine IMessageBus.SendAsync para validar
/// se o pipeline real (não IDbContextOutbox direto) persiste envelope no outbox.
/// Spike-only: NÃO promover para Selecao.Application.
/// </summary>
[SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "SPIKE: Wolverine convenção exige discovery por reflection.")]
public sealed record PublicarEditalSpikeCommand(int Numero, int Ano, string Titulo);

/// <summary>
/// SPIKE V3 — comando que joga exceção depois de AddDomainEvent para validar
/// rollback transacional (entity + envelope ausentes).
/// </summary>
[SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "SPIKE: Wolverine convenção exige discovery por reflection.")]
public sealed record FalharAposPublicarSpikeCommand(int Numero, int Ano, string Titulo);
