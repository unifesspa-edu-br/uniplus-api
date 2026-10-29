namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lê o pedido: só as partes e quem decide uma etapa ou administra concessões o veem.</summary>
public sealed record ObterPedidoDeConcessaoQuery(Guid Id) : IQuery<Result<PedidoDeConcessaoDto>>;

/// <summary>Lê a trilha do pedido, com a mesma regra de visibilidade do detalhe.</summary>
public sealed record ObterTrilhaDoPedidoQuery(Guid Id) : IQuery<Result<IReadOnlyList<EventoDaTrilhaDto>>>;
