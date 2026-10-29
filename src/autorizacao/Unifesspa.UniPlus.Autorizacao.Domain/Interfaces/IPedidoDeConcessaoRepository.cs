namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>A lista de pedidos que a pessoa pediu: a dos que ela pode ver ou a de uma fila de aprovador.</summary>
public enum FilaDePedidos
{
    /// <summary>Os pedidos em que a pessoa é solicitante ou beneficiária.</summary>
    Meus = 1,

    /// <summary>Os pedidos que aguardam a primeira aprovação, a do gestor de acessos.</summary>
    Gestor = 2,

    /// <summary>Os pedidos que aguardam a segunda aprovação, a da encarregada de dados.</summary>
    Encarregada = 3,

    /// <summary>Os pedidos em qualquer situação, para o aprovador reencontrar o que já decidiu ou viu encerrar.</summary>
    Todos = 4,
}

/// <summary>
/// Filtros da lista de pedidos. A pessoa é o par emissor e <c>sub</c>; o e-mail só vale quando verificado,
/// para achar o pedido que a nomeou antes de ela aceitar.
/// </summary>
public sealed record FiltroDePedidos(
    FilaDePedidos Fila,
    SituacaoDoPedido? Situacao,
    Identidade Pessoa,
    string? EmailVerificado);

/// <summary>Pedido com o nome do perfil pedido, para a resposta.</summary>
public sealed record PedidoComPerfil(PedidoDeConcessao Pedido, string PerfilNome) : IIdentificavel
{
    /// <summary>Id do pedido, exposto para a paginação keyset operar sobre a projeção.</summary>
    public Guid Id { get; init; } = Pedido.Id;
}

/// <summary>Repositório dos pedidos de concessão, de seus anexos e dos acessos a eles.</summary>
public interface IPedidoDeConcessaoRepository : IRepository<PedidoDeConcessao>
{
    /// <summary>O pedido com decisões e anexos, rastreado, para quem o altera.</summary>
    Task<PedidoDeConcessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>O pedido com decisões e anexos, sem rastreamento.</summary>
    Task<PedidoDeConcessao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>O anexo, rastreado, para registrar o resultado do verificador.</summary>
    Task<AnexoDoPedido?> ObterAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default);

    Task AdicionarLeituraAsync(LeituraDoAnexoDoPedido leitura, CancellationToken cancellationToken = default);

    /// <summary>Os acessos aos anexos do pedido, do mais antigo ao mais recente, sem rastreamento.</summary>
    Task<IReadOnlyList<LeituraDoAnexoDoPedido>> ListarLeiturasAsync(Guid pedidoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os pedidos paginados por cursor keyset bidirecional ordenado (ADR-0026 + ADR-0094), do mais recente ao mais
    /// antigo (<c>CriadoEm</c> decrescente, <c>Id</c> de desempate), sem rastreamento. A âncora é o par (instante, Id).
    /// </summary>
    Task<(IReadOnlyList<PedidoComPerfil> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        FiltroDePedidos filtro,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);
}
