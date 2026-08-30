namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// Repositório da entidade <see cref="CategoriaDocumento"/> (ADR-0054: banco
/// isolado <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção.
/// </summary>
public interface ICategoriaDocumentoRepository
{
    /// <summary>Carrega a categoria rastreada pelo contexto, para mutação.</summary>
    Task<CategoriaDocumento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a categoria para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<CategoriaDocumento?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista as categorias vivas na ordem de exibição do catálogo — <c>Ordem</c>
    /// primeiro, código como desempate estável. Sem paginação: é catálogo de
    /// referência fechado e de baixo volume, consumido inteiro por carregamento de
    /// tela.
    /// </summary>
    Task<IReadOnlyList<CategoriaDocumento>> ListarVivasOrdenadasAsync(CancellationToken cancellationToken);

    Task AdicionarAsync(CategoriaDocumento categoria, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a categoria para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(CategoriaDocumento categoria);

    /// <summary>
    /// Verifica se existe categoria viva com o <paramref name="codigo"/>
    /// (case-sensitive, sobre o valor normalizado por <c>Trim</c>), excluindo
    /// opcionalmente um <paramref name="excluirId"/> (para a checagem na atualização).
    /// </summary>
    Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken);
}
