namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Resposta do passo 1 (iniciar upload) — Story #759, T3 #784.</summary>
public sealed record IniciarUploadDocumentoEditalDto(
    Guid DocumentoEditalId,
    Uri UrlUpload,
    DateTimeOffset ExpiraEm);

/// <summary>Resposta do passo 3 (confirmar upload) — Story #759, T3 #784.</summary>
public sealed record DocumentoEditalDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    string Status,
    long TamanhoBytes,
    string HashSha256,
    DateTimeOffset ConfirmadoEm);
