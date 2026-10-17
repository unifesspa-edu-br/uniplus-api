namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 3 do envio do documento a publicar: lê o objeto do armazenamento, valida o conteúdo
/// contra o formato declarado, calcula o SHA-256 no servidor e finaliza o registro como imutável.
/// </summary>
public sealed record ConfirmarEnvioDoDocumentoAPublicarCommand(
    Guid ProcessoSeletivoId,
    Guid DocumentoAPublicarId) : ICommand<Result<DocumentoAPublicarDto>>;
