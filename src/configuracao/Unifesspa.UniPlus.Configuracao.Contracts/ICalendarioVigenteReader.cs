namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo do calendário de dias úteis vigente (ADR-0056, UNI-REQ-0080).
/// Expõe a pergunta "há dataset vigente?" e suas datas, para consumo pelo motor de
/// contagem de prazos em dias úteis (módulo de Recursos, RN13 — escopo futuro), sem
/// acesso direto ao banco de Configuração (ADR-0054).
/// </summary>
public interface ICalendarioVigenteReader
{
    /// <summary>Obtém o dataset vigente, ou <see langword="null"/> se nenhum foi marcado vigente ainda.</summary>
    Task<CalendarioVigenteView?> ObterVigenteAsync(CancellationToken cancellationToken = default);
}
