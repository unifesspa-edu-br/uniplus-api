namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="ModeloDeDocumento"/> — independente de
/// <see cref="IProcessoSeletivoRepository"/> porque o modelo não é filho do agregado.
/// </summary>
public interface IModeloDeDocumentoRepository : IRepository<ModeloDeDocumento>
{
    /// <summary>
    /// Reivindica atomicamente a confirmação do modelo — <c>UPDATE ... WHERE id = @id AND status
    /// = Pendente</c>, sem passar pelo change tracker. Duas confirmações concorrentes nunca ganham
    /// as duas: a perdedora afeta zero linhas e não chega a escrever no storage.
    /// </summary>
    Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default);
}
