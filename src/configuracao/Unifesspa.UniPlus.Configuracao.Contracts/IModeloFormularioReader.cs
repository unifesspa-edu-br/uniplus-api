namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo dos modelos de formulário (ADR-0056, UNI-REQ-0144): a Seleção escolhe um
/// modelo e copia o conteúdo dele para o formulário do processo. Somente leitura.
/// </summary>
public interface IModeloFormularioReader
{
    /// <summary>
    /// Os modelos ativos que servem ao tipo de processo — os do próprio tipo e os que servem a
    /// todos — para a finalidade em token canônico, ordenados por código. Finalidade desconhecida
    /// não tem modelo.
    /// </summary>
    Task<IReadOnlyList<ModeloFormularioView>> ListarAtivosAsync(
        string tipoProcessoCodigo, string finalidade, CancellationToken cancellationToken = default);

    /// <summary>O modelo, ativo ou desativado; quem o copia decide pelo estado.</summary>
    Task<ModeloFormularioView?> ObterAsync(Guid id, CancellationToken cancellationToken = default);
}
