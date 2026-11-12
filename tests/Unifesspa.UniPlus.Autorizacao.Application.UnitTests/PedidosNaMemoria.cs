namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório de pedidos com um pedido só, que se obtém para alterar, e os pedidos adicionados.</summary>
internal sealed class PedidosNaMemoria(PedidoDeConcessao? pedido = null) : IPedidoDeConcessaoRepository
{
    public List<PedidoDeConcessao> Adicionados { get; } = [];

    public Task<PedidoDeConcessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(pedido?.Id == id ? pedido : null);

    public Task AdicionarAsync(PedidoDeConcessao entity, CancellationToken cancellationToken = default)
    {
        Adicionados.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PedidoDeConcessao>> ListarAtivosDoPerfilParaAlterarAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<PedidoDeConcessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<IReadOnlyList<PedidoDeConcessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public void Atualizar(PedidoDeConcessao entity) => throw new InvalidOperationException();
    public void Remover(PedidoDeConcessao entity) => throw new InvalidOperationException();
    public Task<Guid?> ObterPerfilIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<PedidoDeConcessao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<AnexoDoPedido?> ObterAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task AdicionarLeituraAsync(LeituraDoAnexoDoPedido leitura, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<IReadOnlyList<LeituraDoAnexoDoPedido>> ListarLeiturasAsync(Guid pedidoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task<(IReadOnlyList<PedidoComPerfil> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        FiltroDePedidos filtro, string? afterSortKey, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
}
