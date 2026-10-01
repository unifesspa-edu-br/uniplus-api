namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Escrita e leitura de manutenção dos modelos de formulário (UNI-REQ-0144).</summary>
public interface IModeloFormularioRepository
{
    /// <summary>O modelo rastreado para mutação.</summary>
    Task<ModeloFormulario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ModeloFormulario?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista paginada por cursor, com filtros opcionais. O filtro de tipo de processo traz os modelos
    /// que servem a ele: os do próprio tipo e os que servem a todos.
    /// </summary>
    Task<(IReadOnlyList<ModeloFormulario> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? tipoProcessoCodigo,
        FinalidadeFormulario? finalidade,
        bool? ativo,
        CancellationToken cancellationToken);

    Task AdicionarAsync(ModeloFormulario modelo, CancellationToken cancellationToken);

    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);
}
