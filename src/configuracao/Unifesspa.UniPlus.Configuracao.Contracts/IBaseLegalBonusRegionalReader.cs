namespace Unifesspa.UniPlus.Configuracao.Contracts;

public interface IBaseLegalBonusRegionalReader
{
    Task<BaseLegalBonusRegionalView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A base legal viva que tem o código, ou nulo: código de base removida ou inexistente não resolve.</summary>
    Task<BaseLegalBonusRegionalView?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken);
}
