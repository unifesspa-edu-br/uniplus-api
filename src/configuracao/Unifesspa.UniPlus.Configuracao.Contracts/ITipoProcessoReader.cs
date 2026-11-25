namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Lê exclusivamente tipos de processo seletivo ativos (UNI-REQ-0098).</summary>
public interface ITipoProcessoReader
{
    Task<IReadOnlyList<TipoProcessoView>> ListarAtivosAsync(CancellationToken cancellationToken = default);
    Task<TipoProcessoView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Busca pelo código exato, sem aparar nem normalizar.</summary>
    Task<TipoProcessoView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}
