namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Delega tudo ao repositório real, exceto as conferências de unicidade entre as Unidades vivas, que respondem
/// "não existe", como responderiam se outra transação gravasse a Unidade conflitante logo depois da conferência.
/// </summary>
public sealed class UnidadeComConsultaObsoleta(UnidadeRepository real, ConsultasAoCadastroContadas? contagem = null)
    : IUnidadeRepository
{
    public Task<bool> SlugExisteEntreVivosAsync(Slug slug, Guid? excluirId, CancellationToken cancellationToken) =>
        RespostaObsoleta();

    public Task<bool> SiglaExisteEntreVivosAsync(string sigla, Guid? excluirId, CancellationToken cancellationToken) =>
        RespostaObsoleta();

    public Task<bool> CodigoExisteEntreVivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken) =>
        RespostaObsoleta();

    public Task<Unidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        real.ObterPorIdAsync(id, cancellationToken);

    public Task<Unidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        real.ObterPorIdParaLeituraAsync(id, cancellationToken);

    public Task<(IReadOnlyList<Unidade> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey, Guid? afterId, int limit, PaginationDirection direction, FiltroListagemUnidades filtro, CancellationToken cancellationToken) =>
        real.ListarPaginadoAsync(afterSortKey, afterId, limit, direction, filtro, cancellationToken);

    public Task AdicionarAsync(Unidade unidade, CancellationToken cancellationToken) =>
        real.AdicionarAsync(unidade, cancellationToken);

    public void Remover(Unidade unidade) => real.Remover(unidade);

    public Task<bool> PossuiSubordinadasVivasAsync(Guid id, CancellationToken cancellationToken) =>
        real.PossuiSubordinadasVivasAsync(id, cancellationToken);

    public Task<bool> EhDescendenteAsync(
        Guid possivelDescendenteId, Guid possivelAncestralId, CancellationToken cancellationToken) =>
        real.EhDescendenteAsync(possivelDescendenteId, possivelAncestralId, cancellationToken);

    private Task<bool> RespostaObsoleta()
    {
        contagem?.Registrar();
        return Task.FromResult(false);
    }
}
