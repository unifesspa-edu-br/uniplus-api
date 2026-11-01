namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Registro que só recebe inclusões dos atos do circuito de aprovação da proteção de dados do catálogo
/// (ADR-0153): grava junto da transação de cada ato e lê o histórico de um item ou as decisões da proteção de dados.
/// </summary>
public interface IAtoDaProtecaoDoCatalogoRepository
{
    /// <summary>Acrescenta o ato à transação em curso; é gravado junto da mudança do item.</summary>
    void Adicionar(AtoDaProtecaoDoCatalogo ato);

    /// <summary>Todos os atos do item, do mais antigo ao mais recente.</summary>
    Task<IReadOnlyList<AtoDaProtecaoDoCatalogo>> ListarDoItemAsync(
        CadastroDoCatalogo cadastro, Guid itemId, CancellationToken cancellationToken);

    /// <summary>
    /// As decisões da proteção de dados (aprovação, ajuste recomendado e reprovação) que passam pelo filtro, paginadas
    /// por cursor keyset ordenado, das mais recentes para as mais antigas.
    /// </summary>
    Task<(IReadOnlyList<AtoDaProtecaoDoCatalogo> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarDecisoesAsync(
        FiltroDasDecisoesDaProtecao filtro,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);
}
