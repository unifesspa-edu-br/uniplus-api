namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

/// <summary>
/// Solicita que o host garanta a existência do próximo disparo diário.
/// </summary>
public sealed record InicializarGatilhoDiarioDiscentes;

/// <summary>
/// Disparo durável da sincronização referente a um dia específico.
/// </summary>
public sealed record GatilhoDiarioDiscentes(DateOnly DataDeReferencia);
