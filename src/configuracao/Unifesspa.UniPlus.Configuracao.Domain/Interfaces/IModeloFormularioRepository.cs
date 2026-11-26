namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Escrita e leitura de manutenção dos modelos de formulário (UNI-REQ-0144).</summary>
public interface IModeloFormularioRepository
{
    /// <summary>O modelo rastreado para mutação.</summary>
    Task<ModeloFormulario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ModeloFormulario?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista paginada por cursor, com filtros opcionais. O filtro de tipo de processo traz os modelos
    /// que servem a ele: os do próprio tipo e os que servem a todos. O filtro de declaração separa os modelos
    /// que servem à fase de inscrição dos que servem a outra fase.
    /// </summary>
    Task<(IReadOnlyList<ModeloFormulario> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? tipoProcessoCodigo,
        bool? serveAFaseDeInscricao,
        bool? ativo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Todos os modelos, ativos e inativos, sem rastreamento: o que a alteração da definição de um fato
    /// pode deixar sem valer, inclusive o modelo inativo que uma reativação traria de volta.
    /// </summary>
    Task<IReadOnlyList<ModeloFormulario>> ListarTodosAsync(CancellationToken cancellationToken);

    Task AdicionarAsync(ModeloFormulario modelo, CancellationToken cancellationToken);

    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
