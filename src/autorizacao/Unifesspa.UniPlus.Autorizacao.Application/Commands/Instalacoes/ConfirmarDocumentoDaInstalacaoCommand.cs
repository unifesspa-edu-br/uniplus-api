namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Passo 2: quem iniciou confirma o PUT com o ETag que ele devolveu; a API põe o PDF em verificação e publica o
/// pedido ao verificador pelo outbox.
/// </summary>
public sealed record ConfirmarDocumentoDaInstalacaoCommand(Guid Id, string? ETag) : ICommand<Result<InstalacaoDto>>;
