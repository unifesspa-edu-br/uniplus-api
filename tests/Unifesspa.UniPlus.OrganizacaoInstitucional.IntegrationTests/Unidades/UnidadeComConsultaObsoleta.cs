namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>
/// Delega tudo ao repositório real, exceto as conferências de unicidade entre as Unidades vivas, que respondem
/// "não existe", como responderiam se outra transação gravasse a Unidade conflitante logo depois da conferência.
/// </summary>
public sealed class UnidadeComConsultaObsoleta(UnidadeRepository real, ConsultasObsoletasContadas? contagem = null)
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

    public Task<(IReadOnlyList<Unidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, FiltroListagemUnidades filtro, CancellationToken cancellationToken) =>
        real.ListarPaginadoAsync(afterId, limit, direction, filtro, cancellationToken);

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

/// <summary>Conta as conferências que o repositório de consulta obsoleta respondeu, para provar que o teste a exercitou.</summary>
public sealed class ConsultasObsoletasContadas
{
    private int _total;

    public int Total => _total;

    public void Registrar() => Interlocked.Increment(ref _total);
}
