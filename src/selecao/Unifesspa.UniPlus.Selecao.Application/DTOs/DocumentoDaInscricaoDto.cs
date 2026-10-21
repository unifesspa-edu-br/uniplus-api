namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resposta do passo 1 do envio de um documento da inscrição. <paramref name="ContentTypeExigido"/> é o
/// valor exato do header <c>Content-Type</c> que o PUT direto ao <paramref name="UrlUpload"/> precisa
/// enviar: a assinatura da URL o inclui. <paramref name="ApresentacaoId"/> é o que o cliente devolve
/// para juntar o verso à frente na mesma apresentação.
/// </summary>
public sealed record IniciarEnvioDeDocumentoDaInscricaoDto(
    Guid ArquivoId,
    Guid ApresentacaoId,
    Uri UrlUpload,
    string ContentTypeExigido,
    DateTimeOffset ExpiraEm);

/// <summary>
/// O arquivo confirmado, como o candidato o enxerga. Nunca leva a chave do objeto, a URL do
/// armazenamento nem o nome original, que não é guardado.
/// </summary>
public sealed record ArquivoDaApresentacaoDto(
    Guid ArquivoId,
    Guid ApresentacaoId,
    Guid ExigenciaId,
    string? OcorrenciaId,
    string TipoDeMidia,
    long TamanhoBytes,
    string HashSha256,
    DateTimeOffset ConfirmadoEm);
