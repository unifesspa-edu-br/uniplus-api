namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Pede a URL assinada de leitura de um anexo verificado; o acesso entra na trilha do pedido.</summary>
public sealed record LerAnexoDoPedidoCommand(Guid PedidoId, Guid AnexoId) : ICommand<Result<LeituraDoAnexoDto>>;
