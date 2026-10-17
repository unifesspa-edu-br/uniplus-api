namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using Enums;

using Errors;

using Services;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um anexo do edital, copiado por valor do documento confirmado (ADR-0061): o nome com que a
/// pessoa o recebe no download, o formato, o tamanho e o hash do arquivo. O documento confirmado é
/// imutável, e é o hash que o edital atesta.
/// </summary>
/// <remarks>
/// O <see cref="Id"/> é a identidade do anexo no edital: o anexo que a retificação herda sem mudança
/// mantém o seu, e o trocado ou renomeado ganha outro. O endereço no acervo inclui também o ato, então
/// cada versão publica o anexo no próprio endereço, e a versão anterior conserva o que divulgou.
/// </remarks>
/// <param name="Id">Identidade do anexo no edital, parte da chave no acervo público.</param>
/// <param name="DocumentoId">O documento a publicar confirmado que o anexo congela.</param>
/// <param name="NomeArquivo">Nome do arquivo no download, com a extensão do formato.</param>
/// <param name="Formato">Formato do arquivo.</param>
/// <param name="TamanhoBytes">Tamanho do arquivo confirmado.</param>
/// <param name="HashSha256">Hash SHA-256 do arquivo confirmado, em hexadecimal minúsculo.</param>
public sealed record AnexoDoEdital(
    Guid Id,
    Guid DocumentoId,
    string NomeArquivo,
    FormatoDeDocumento Formato,
    long TamanhoBytes,
    string HashSha256)
{
    /// <summary>Quantos anexos um edital pode levar.</summary>
    public const int QuantidadeMaxima = 20;

    /// <summary>Tamanho máximo do nome do anexo, com a extensão.</summary>
    public const int NomeArquivoMaxLength = NomeDeArquivoEnviado.TamanhoMaximo;

    /// <summary>O tipo de mídia do arquivo no formato.</summary>
    public string TipoDeMidia => FormatosDeDocumento.TipoDeMidia(Formato);

    /// <summary>
    /// A cópia do documento sob o nome pedido; recusa o que ainda não foi confirmado e o nome que o
    /// download não aceitaria.
    /// </summary>
    public static Result<AnexoDoEdital> Criar(Guid id, DocumentoAPublicar documento, string? nome)
    {
        ArgumentNullException.ThrowIfNull(documento);

        if (documento.Finalidade != FinalidadeDoDocumento.AnexoDoEdital)
        {
            return Result<AnexoDoEdital>.Failure(new DomainError(
                AnexoDoEditalErrorCodes.FinalidadeIncompativel,
                "Somente um documento enviado como anexo do edital pode ser anexado ao edital."));
        }

        if (documento.Status != StatusArquivoEnviado.Confirmado
            || documento.HashSha256 is not { } hash
            || documento.TamanhoBytes is not { } tamanho)
        {
            return Result<AnexoDoEdital>.Failure(new DomainError(
                AnexoDoEditalErrorCodes.NaoConfirmado,
                "Somente um documento confirmado pode ser anexado ao edital."));
        }

        string? nomeArquivo = NomeDeArquivoEnviado.Normalizar(nome, documento.Extensao);
        return nomeArquivo is null
            ? Result<AnexoDoEdital>.Failure(new DomainError(
                AnexoDoEditalErrorCodes.NomeInvalido,
                $"O nome do anexo é obrigatório, tem até {NomeArquivoMaxLength} caracteres com a extensão e não tem separador de caminho, caractere de controle nem {NomeDeArquivoEnviado.CaracteresProibidos}."))
            : Result<AnexoDoEdital>.Success(new AnexoDoEdital(id, documento.Id, nomeArquivo, documento.Formato, tamanho, hash));
    }
}
