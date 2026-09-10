namespace Unifesspa.UniPlus.Configuracao.Contracts;

public interface IBaseLegalBonusRegionalReader
{
    Task<BaseLegalBonusRegionalView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
}
