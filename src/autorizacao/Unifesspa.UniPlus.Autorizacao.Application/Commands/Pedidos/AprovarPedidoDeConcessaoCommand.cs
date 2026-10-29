namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Aprova a etapa que o pedido aguarda; o sistema descobre a etapa pela situação do pedido.</summary>
public sealed record AprovarPedidoDeConcessaoCommand(Guid Id) : ICommand<Result<PedidoDeConcessaoDto>>;
