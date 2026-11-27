namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>Delega tudo ao repositório real de Unidades e conta cada chamada recebida.</summary>
public sealed class UnidadeRepositorioContado(UnidadeRepository real, ConsultasAoCadastroContadas contagem)
    : IUnidadeRepository
{
    public Task<Unidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.ObterPorIdAsync(id, cancellationToken);
    }

    public Task<Unidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.ObterPorIdParaLeituraAsync(id, cancellationToken);
    }

    public Task<(IReadOnlyList<Unidade> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey, Guid? afterId, int limit, PaginationDirection direction, FiltroListagemUnidades filtro, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.ListarPaginadoAsync(afterSortKey, afterId, limit, direction, filtro, cancellationToken);
    }

    public Task AdicionarAsync(Unidade unidade, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.AdicionarAsync(unidade, cancellationToken);
    }

    public void Remover(Unidade unidade)
    {
        contagem.Registrar();
        real.Remover(unidade);
    }

    public Task<bool> SlugExisteEntreVivosAsync(Slug slug, Guid? excluirId, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.SlugExisteEntreVivosAsync(slug, excluirId, cancellationToken);
    }

    public Task<bool> SiglaExisteEntreVivosAsync(string sigla, Guid? excluirId, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.SiglaExisteEntreVivosAsync(sigla, excluirId, cancellationToken);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.CodigoExisteEntreVivosAsync(codigo, excluirId, cancellationToken);
    }

    public Task<bool> PossuiSubordinadasVivasAsync(Guid id, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.PossuiSubordinadasVivasAsync(id, cancellationToken);
    }

    public Task<bool> EhDescendenteAsync(
        Guid possivelDescendenteId, Guid possivelAncestralId, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.EhDescendenteAsync(possivelDescendenteId, possivelAncestralId, cancellationToken);
    }
}
