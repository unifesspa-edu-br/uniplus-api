namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Recusa o pedido na etapa que ele aguarda, com motivo; encerra o pedido sem concessão.</summary>
public sealed record RecusarPedidoDeConcessaoCommand(Guid Id, string? Motivo) : ICommand<Result<PedidoDeConcessaoDto>>;
