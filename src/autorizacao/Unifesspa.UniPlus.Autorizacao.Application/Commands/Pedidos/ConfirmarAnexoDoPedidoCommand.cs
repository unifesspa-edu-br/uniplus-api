namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O solicitante confirma o PUT com o ETag que ele devolveu; a API põe o PDF em verificação e publica o pedido ao
/// verificador pelo outbox.
/// </summary>
public sealed record ConfirmarAnexoDoPedidoCommand(Guid PedidoId, Guid AnexoId, string? ETag) : ICommand<Result<AnexoDoPedidoDto>>;
