using Unifesspa.UniPlus.Discentes.Domain.Entities;

namespace Unifesspa.UniPlus.Discentes.Domain.Interfaces;

public interface IVinculoDiscenteRepository
{
    Task<VinculoDiscente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VinculoDiscente?> ObterPorIdSigaaAsync(long idDiscenteSigaa, CancellationToken cancellationToken = default);
    Task AdicionarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assíncrono porque o CPF é recifrado a cada atualização (ADR-0121) — a
    /// implementação chama <c>IUniPlusEncryptionService</c>, que não tem versão
    /// síncrona.
    /// </summary>
    Task AtualizarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default);
}
