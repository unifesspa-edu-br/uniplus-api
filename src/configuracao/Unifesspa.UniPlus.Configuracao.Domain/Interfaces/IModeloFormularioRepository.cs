namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Filtros opcionais da lista de manutenção dos modelos. O filtro de tipo de processo traz os modelos que servem a ele:
/// os do próprio tipo e os que servem a todos. O de declaração separa os que servem à fase de inscrição dos que servem a
/// outra fase.
/// </summary>
public sealed record FiltroDosModelosDeFormulario(string? TipoProcessoCodigo, bool? ServeAFaseDeInscricao, bool? Ativo);

/// <summary>Escrita e leitura de manutenção dos modelos de formulário (UNI-REQ-0144).</summary>
public interface IModeloFormularioRepository
{
    /// <summary>O modelo rastreado para mutação.</summary>
    Task<ModeloFormulario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ModeloFormulario?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lista paginada por cursor, com os filtros opcionais informados.</summary>
    Task<(IReadOnlyList<ModeloFormulario> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroDosModelosDeFormulario filtro,
        CancellationToken cancellationToken);

    /// <summary>
    /// Todos os modelos, ativos e inativos, sem rastreamento: o que a alteração da definição de um fato
    /// pode deixar sem valer, inclusive o modelo inativo que uma reativação traria de volta.
    /// </summary>
    Task<IReadOnlyList<ModeloFormulario>> ListarTodosAsync(CancellationToken cancellationToken);

    Task AdicionarAsync(ModeloFormulario modelo, CancellationToken cancellationToken);

    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
