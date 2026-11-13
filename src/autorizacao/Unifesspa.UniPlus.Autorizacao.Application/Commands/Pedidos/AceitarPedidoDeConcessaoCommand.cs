namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O beneficiário aceita o pedido que o nomeou e, no pedido protegido, o termo fixado nele; o aceite grava a identidade e o
/// nome do token dele.
/// </summary>
/// <param name="TermoVersaoApresentadaId">A versão do termo apresentada; obrigatória no pedido com termo e proibida nos demais.</param>
public sealed record AceitarPedidoDeConcessaoCommand(Guid Id, Guid? TermoVersaoApresentadaId = null) : ICommand<Result<PedidoDeConcessaoDto>>;
