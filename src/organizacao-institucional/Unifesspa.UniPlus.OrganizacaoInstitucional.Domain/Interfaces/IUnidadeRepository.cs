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
    Task<Unidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Unidade>> ListarAtivasAsync(CancellationToken cancellationToken);

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
    /// Verifica se definir <paramref name="candidatoSuperiorId"/> como superior de
    /// <paramref name="unidadeId"/> formaria ciclo na hierarquia (o candidato é
    /// descendente ou igual à unidade).
    /// </summary>
    Task<bool> FormariaCicloAsync(Guid unidadeId, Guid candidatoSuperiorId, CancellationToken cancellationToken);
}
