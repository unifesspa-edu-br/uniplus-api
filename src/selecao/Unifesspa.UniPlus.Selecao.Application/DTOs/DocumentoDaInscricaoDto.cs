using System.Text;

namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;

using Services;

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
/// <param name="Rotulo">Como a tela chama o arquivo: "Arquivo N", a posição entre os da mesma exigência e ocorrência, por ordem de confirmação.</param>
/// <param name="NomeParaDownload">O nome com que o arquivo é baixado: o rótulo, o nome do documento e o início do SHA-256, mais a
/// extensão, para que arquivos diferentes não se sobrescrevam; nulo enquanto o arquivo não está verificado.</param>
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
    string? Mensagem,
    string Rotulo,
    string? NomeParaDownload)
{
    private const int TamanhoDoHashNoNome = 8;
    /// <summary>Cabe, com o rótulo, o hash e a extensão, nos 255 bytes que os sistemas de arquivos aceitam, mesmo com acentos.</summary>
    private const int TamanhoMaximoDoNomeDoDocumento = 60;

    public static ArquivoDaApresentacaoDto De(ArquivoDaApresentacao arquivo, ApresentacaoDeDocumento apresentacao, int posicao)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(apresentacao);

        string rotulo = $"Arquivo {posicao}";
        return new ArquivoDaApresentacaoDto(
            arquivo.Id, arquivo.ApresentacaoId, apresentacao.ExigenciaId, apresentacao.OcorrenciaId,
            arquivo.TipoDeMidia, arquivo.TamanhoBytes, arquivo.HashSha256, arquivo.ConfirmadoEm!.Value,
            arquivo.Situacao, arquivo.MensagemAoCandidato, rotulo, NomeDeDownload(arquivo, apresentacao, rotulo));
    }

    private static string? NomeDeDownload(ArquivoDaApresentacao arquivo, ApresentacaoDeDocumento apresentacao, string rotulo) =>
        arquivo.Situacao == SituacaoDoArquivoDoCandidato.Verificado && arquivo.HashSha256 is { } hash
            ? $"{rotulo} - {NomeSeguroParaArquivo(apresentacao.NomeDoDocumento)} - {hash[..Math.Min(TamanhoDoHashNoNome, hash.Length)].ToLowerInvariant()}"
                + $".{FormatosDoArquivoDoCandidato.Extensao(arquivo.Formato)}"
            : null;

    /// <summary>O nome do documento em NFC, sem o que o sistema de arquivos recusa (as mesmas regras do nome enviado pelo administrador).</summary>
    private static string NomeSeguroParaArquivo(string nome)
    {
        string limpo = string.Join(' ', new string([.. nome.Normalize(NormalizationForm.FormC)
            .Select(static c => char.IsControl(c) || NomeDeArquivoEnviado.CaracteresProibidos.Contains(c, StringComparison.Ordinal) ? ' ' : c)])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return (limpo.Length > TamanhoMaximoDoNomeDoDocumento ? limpo[..TamanhoMaximoDoNomeDoDocumento] : limpo).TrimEnd();
    }
}

/// <summary>
/// A URL pré-assinada, de validade curta, do arquivo verificado no bucket definitivo.
/// </summary>
public sealed record AcessoArquivoDoCandidatoDto(Uri Url, DateTimeOffset ExpiraEm);
