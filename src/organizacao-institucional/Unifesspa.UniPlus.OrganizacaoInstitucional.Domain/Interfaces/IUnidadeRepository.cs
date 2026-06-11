namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Repositório da entidade <see cref="Unidade"/> (ADR-0054: banco isolado de
/// Organização). Todas as operações de leitura excluem registros soft-deleted
/// via query filter do EF Core.
/// </summary>
public interface IUnidadeRepository
{
    /// <summary>
    /// Carrega a unidade com o histórico de identificadores incluído, rastreada
    /// pelo contexto — para mutação (atualização que renomeia identificadores) e
    /// verificações que dependem do agregado completo.
    /// </summary>
    Task<Unidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Carrega a unidade para leitura (<c>AsNoTracking</c>, sem o histórico de
    /// identificadores) — para projeção em DTO. Evita o over-fetch da coleção de
    /// histórico em caminhos que não a expõem.
    /// </summary>
    Task<Unidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista unidades ativas paginadas por cursor keyset (ADR-0026): ordena por
    /// <c>Id</c> (Guid v7, ADR-0032 — ordem cronológica) e retorna até
    /// <paramref name="take"/> itens com <c>Id</c> maior que
    /// <paramref name="afterId"/> (ou a primeira janela quando <c>null</c>).
    /// </summary>
    Task<IReadOnlyList<Unidade>> ListarPaginadoAsync(Guid? afterId, int take, CancellationToken cancellationToken);

    Task AdicionarAsync(Unidade unidade, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a unidade para remoção no contexto EF. O
    /// <c>SoftDeleteInterceptor</c> converte automaticamente para soft-delete
    /// preenchendo <c>DeletedBy</c>/<c>DeletedAt</c> a partir do
    /// <c>IUserContext</c> e do <c>TimeProvider</c>.
    /// </summary>
    void Remover(Unidade unidade);

    /// <summary>Verifica se existe unidade viva com o slug informado, excluindo opcionalmente um Id.</summary>
    Task<bool> SlugExisteEntreLivosAsync(Slug slug, Guid? excluirId, CancellationToken cancellationToken);

    /// <summary>Verifica se existe unidade viva com a sigla informada (case-insensitive), excluindo opcionalmente um Id.</summary>
    Task<bool> SiglaExisteEntreLivosAsync(string sigla, Guid? excluirId, CancellationToken cancellationToken);

    /// <summary>Verifica se existe unidade viva com o código informado, excluindo opcionalmente um Id.</summary>
    Task<bool> CodigoExisteEntreLivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken);

    /// <summary>Verifica se a unidade tem subordinadas vivas (impede remoção).</summary>
    Task<bool> PossuiSubordinadasVivasAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Indica se <paramref name="possivelDescendenteId"/> é descendente (ou igual)
    /// de <paramref name="possivelAncestralId"/> na hierarquia, percorrendo a
    /// cadeia de superiores. O handler usa isto para barrar a atribuição de um
    /// superior que formaria ciclo (superior que é descendente da própria unidade).
    /// </summary>
    Task<bool> EhDescendenteAsync(Guid possivelDescendenteId, Guid possivelAncestralId, CancellationToken cancellationToken);
}
