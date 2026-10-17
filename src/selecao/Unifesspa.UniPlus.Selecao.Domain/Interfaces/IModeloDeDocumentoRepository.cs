namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="ModeloDeDocumento"/> — independente de
/// <see cref="IProcessoSeletivoRepository"/> porque o modelo não é filho do agregado.
/// </summary>
public interface IModeloDeDocumentoRepository : IRepository<ModeloDeDocumento>, IArquivoEnviadoRepository
{
    /// <summary>Os modelos do processo entre os ids dados, numa consulta só, sem rastreamento.</summary>
    Task<IReadOnlyList<ModeloDeDocumento>> ListarDoProcessoAsync(
        Guid processoSeletivoId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
