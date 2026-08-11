namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Lê exclusivamente tipos de etapa ativos (UNI-REQ-0015, UNI-REQ-0087).</summary>
public interface ITipoEtapaReader
{
    Task<IReadOnlyList<TipoEtapaView>> ListarAtivosAsync(CancellationToken cancellationToken = default);
    Task<TipoEtapaView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TipoEtapaView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}
