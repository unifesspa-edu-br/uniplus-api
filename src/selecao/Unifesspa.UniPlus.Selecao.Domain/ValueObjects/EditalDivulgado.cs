namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os documentos de um ato do processo, divulgados no acervo público: o edital de uma versão do
/// certame, pelo ato que a criou, ou o documento de um ato que não cria versão.
/// </summary>
/// <remarks>
/// O endereço fica registrado no instante da divulgação porque a chave no acervo depende do ano e
/// do tipo do ato, que só chegam com o registro do ato e que Seleção não guarda: depois, não há
/// como recalculá-la.
/// </remarks>
/// <param name="AtoId">O ato normativo que divulgou os documentos.</param>
/// <param name="NumeroVersao">A versão de configuração que o ato criou; nulo no ato que não cria versão.</param>
/// <param name="UrlDownload">Endereço do documento principal no acervo público, o mesmo que o documento divulgado informa.</param>
/// <param name="Documentos">
/// Os documentos do ato no acervo — o edital como principal e os anexos em seguida. Nulo no registro
/// gravado antes de existirem anexos, e lido como lista vazia.
/// </param>
public sealed record EditalDivulgado(
    Guid AtoId,
    int? NumeroVersao,
    string UrlDownload,
    IReadOnlyList<DocumentoDivulgado>? Documentos = null);

/// <summary>
/// Um documento de um ato, como o acervo público o serve: o papel que ele tem no ato, o nome com que
/// é baixado, o tipo de mídia, o tamanho e o endereço.
/// </summary>
/// <param name="Papel"><see cref="PapelDoDocumentoDivulgado.Principal"/> ou <see cref="PapelDoDocumentoDivulgado.Anexo"/>.</param>
/// <param name="Nome">Nome do arquivo no download.</param>
/// <param name="TipoDeMidia">Tipo de mídia do arquivo.</param>
/// <param name="TamanhoBytes">Tamanho do arquivo.</param>
/// <param name="HashSha256">Hash SHA-256 do arquivo, em hexadecimal minúsculo.</param>
/// <param name="UrlDownload">Endereço do arquivo no acervo público.</param>
public sealed record DocumentoDivulgado(
    string Papel,
    string Nome,
    string TipoDeMidia,
    long TamanhoBytes,
    string HashSha256,
    string UrlDownload);

/// <summary>Os papéis que um documento tem no ato que o publica, como o contrato público os nomeia.</summary>
public static class PapelDoDocumentoDivulgado
{
    /// <summary>O documento que é o próprio ato — o edital.</summary>
    public const string Principal = "principal";

    /// <summary>O documento que acompanha o ato.</summary>
    public const string Anexo = "anexo";
}
