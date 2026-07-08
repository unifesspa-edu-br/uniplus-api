namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resposta do passo 1 (iniciar upload) — Story #759, T3 #784.
/// <paramref name="ContentTypeExigido"/> é o valor exato do header
/// <c>Content-Type</c> que o PUT direto ao <see cref="UrlUpload"/> precisa
/// enviar — a assinatura SigV4 da URL o inclui como header assinado, então
/// qualquer variação (ex.: <c>application/pdf; charset=utf-8</c>) faz o
/// MinIO rejeitar com SignatureDoesNotMatch antes de chegar à validação de
/// negócio.
/// </summary>
public sealed record IniciarUploadDocumentoEditalDto(
    Guid DocumentoEditalId,
    Uri UrlUpload,
    string ContentTypeExigido,
    DateTimeOffset ExpiraEm);

/// <summary>Resposta do passo 3 (confirmar upload) — Story #759, T3 #784.</summary>
public sealed record DocumentoEditalDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    string Status,
    long TamanhoBytes,
    string HashSha256,
    DateTimeOffset ConfirmadoEm);
