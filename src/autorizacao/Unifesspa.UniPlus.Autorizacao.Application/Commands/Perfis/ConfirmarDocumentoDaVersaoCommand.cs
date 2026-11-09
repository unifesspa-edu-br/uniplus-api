namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confirma o PUT com o ETag que ele devolveu; a API põe o PDF em verificação e publica o pedido ao verificador pelo
/// outbox.
/// </summary>
public sealed record ConfirmarDocumentoDaVersaoCommand(Guid PerfilId, Guid VersaoId, string? ETag) : ICommand<Result<DocumentoDaVersaoDto>>;
