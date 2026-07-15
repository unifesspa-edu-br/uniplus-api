namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo do catálogo <c>fato_candidato</c> (ADR-0056, ADR-0111).
/// Expõe o vocabulário fechado de fatos do candidato para consumo por outros
/// bounded contexts (ex.: o validador de predicado de desempate e o gatilho de
/// exigência documental do Módulo Seleção) sem acesso direto ao banco de
/// Configuração. Somente leitura — o catálogo é seed-governado e append-only.
/// </summary>
public interface IFatoCandidatoReader
{
    /// <summary>
    /// Lista todos os fatos do catálogo, ordenados por <c>Codigo</c> ascendente para
    /// determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<FatoCandidatoView>> ListarAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve um fato pela sua <b>chave natural</b> — o consumidor tem o código
    /// embutido no predicado, nunca um <see cref="Guid"/>. Retorna
    /// <see langword="null"/> se o código não existir no vocabulário.
    /// </summary>
    Task<FatoCandidatoView?> ObterPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);
}
