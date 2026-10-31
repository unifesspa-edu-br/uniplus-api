namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Aprova a etapa que o pedido aguarda; o sistema descobre a etapa pela situação do pedido.</summary>
/// <param name="ValidaAte">Fim da vigência da concessão, definido pelo gestor na etapa dele; nulo, sem prazo.</param>
public sealed record AprovarPedidoDeConcessaoCommand(Guid Id, DateTimeOffset? ValidaAte = null) : ICommand<Result<PedidoDeConcessaoDto>>;
