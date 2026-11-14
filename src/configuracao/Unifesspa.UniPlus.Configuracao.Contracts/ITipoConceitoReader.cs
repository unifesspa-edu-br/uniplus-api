namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Lê exclusivamente tipos de conceito ativos.</summary>
public interface ITipoConceitoReader
{
    Task<TipoConceitoView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TipoConceitoView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}
