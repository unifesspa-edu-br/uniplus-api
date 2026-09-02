namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

/// <summary>
/// Pedido para reconciliar a réplica de vínculos com o que o SIGAA tem hoje.
/// </summary>
/// <param name="DataDeReferencia">
/// Dia a que a execução se refere. É por ele que se reconhece que o dia já foi
/// sincronizado, e não pelo instante em que a mensagem foi processada.
/// </param>
public sealed record SincronizarVinculosDiscentes(DateOnly DataDeReferencia);
