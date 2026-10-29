namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O solicitante cancela o pedido, só antes da primeira decisão.</summary>
public sealed record CancelarPedidoDeConcessaoCommand(Guid Id) : ICommand<Result>;
