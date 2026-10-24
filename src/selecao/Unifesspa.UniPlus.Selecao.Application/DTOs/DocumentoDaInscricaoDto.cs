namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Entities;
using Domain.Enums;

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
/// O arquivo confirmado como o candidato o enxerga, com a situação e a mensagem. Tamanho e SHA-256 só existem no
/// verificado. Nunca leva a chave do objeto, a URL do armazenamento nem o nome original.
/// </summary>
/// <param name="Mensagem">O que dizer ao candidato (UNI-REQ-0027); nula quando o arquivo está verificado.</param>
public sealed record ArquivoDaApresentacaoDto(
    Guid ArquivoId,
    Guid ApresentacaoId,
    Guid ExigenciaId,
    string? OcorrenciaId,
    string TipoDeMidia,
    long? TamanhoBytes,
    string? HashSha256,
    DateTimeOffset ConfirmadoEm,
    SituacaoDoArquivoDoCandidato Situacao,
    string? Mensagem)
{
    public static ArquivoDaApresentacaoDto De(ArquivoDaApresentacao arquivo, ApresentacaoDeDocumento apresentacao)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(apresentacao);

        return new ArquivoDaApresentacaoDto(
            arquivo.Id, arquivo.ApresentacaoId, apresentacao.ExigenciaId, apresentacao.OcorrenciaId,
            arquivo.TipoDeMidia, arquivo.TamanhoBytes, arquivo.HashSha256, arquivo.ConfirmadoEm!.Value,
            arquivo.Situacao, arquivo.MensagemAoCandidato);
    }
}

/// <summary>
/// A URL pré-assinada, de validade curta, do arquivo verificado no bucket definitivo.
/// </summary>
public sealed record AcessoArquivoDoCandidatoDto(Uri Url, DateTimeOffset ExpiraEm);
