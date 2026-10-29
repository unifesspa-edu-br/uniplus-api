namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O beneficiário aceita o pedido que o nomeou; o aceite grava a identidade e o nome do token dele.</summary>
public sealed record AceitarPedidoDeConcessaoCommand(Guid Id) : ICommand<Result<PedidoDeConcessaoDto>>;
