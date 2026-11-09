namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório de pedidos que só sabe listar os ativos de um perfil, para os testes da troca de vigência.</summary>
internal sealed class PedidosAtivosNaMemoria(params PedidoDeConcessao[] ativos) : IPedidoDeConcessaoRepository
{
    public Task<IReadOnlyList<PedidoDeConcessao>> ListarAtivosDoPerfilParaAlterarAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PedidoDeConcessao>>([.. ativos.Where(p => p.PerfilId == perfilId)]);

    public Task<PedidoDeConcessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<IReadOnlyList<PedidoDeConcessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task AdicionarAsync(PedidoDeConcessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public void Atualizar(PedidoDeConcessao entity) => throw new InvalidOperationException();
    public void Remover(PedidoDeConcessao entity) => throw new InvalidOperationException();
    public Task<PedidoDeConcessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<Guid?> ObterPerfilIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<PedidoDeConcessao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<AnexoDoPedido?> ObterAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task AdicionarLeituraAsync(LeituraDoAnexoDoPedido leitura, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<IReadOnlyList<LeituraDoAnexoDoPedido>> ListarLeiturasAsync(Guid pedidoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task<(IReadOnlyList<PedidoComPerfil> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        FiltroDePedidos filtro, string? afterSortKey, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
}
