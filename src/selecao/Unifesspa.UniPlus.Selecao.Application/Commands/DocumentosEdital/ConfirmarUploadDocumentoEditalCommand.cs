namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 3 do fluxo de upload direto do documento do Edital (Story #759, T3
/// #784): lê o objeto do MinIO, valida content-type/tamanho/assinatura,
/// calcula o sha256 server-side e finaliza o registro como imutável.
/// </summary>
public sealed record ConfirmarUploadDocumentoEditalCommand(
    Guid ProcessoSeletivoId,
    Guid DocumentoEditalId) : ICommand<Result<DocumentoEditalDto>>;
