namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O solicitante pede a URL de PUT assinada de um PDF para anexar ao pedido, antes da primeira decisão.</summary>
public sealed record CriarAnexoDoPedidoCommand(Guid PedidoId, long TamanhoDoPdf) : ICommand<Result<AnexoParaEnvioDto>>;
