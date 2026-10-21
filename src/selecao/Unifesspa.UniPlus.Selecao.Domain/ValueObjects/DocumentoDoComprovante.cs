namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma exigência documental da inscrição como o comprovante a registra (UNI-REQ-0029): a obrigatoriedade,
/// a situação no instante do envio, a consequência de não atender escrita por extenso e, de cada arquivo,
/// só o que o identifica sem o revelar. Entra no hash de integridade do comprovante.
/// </summary>
/// <param name="Situacao">O token de <see cref="Enums.SituacaoDoDocumento"/> (<c>SATISFEITA</c>, <c>PENDENTE</c>, ...) ou <c>DISPENSADA</c>: a alternativa que um grupo "um ou outro" já atendido dispensou.</param>
/// <param name="Consequencia">O que acontece a quem não atende; nulo quando a exigência não declara consequência.</param>
public sealed record DocumentoDoComprovante(
    Guid ExigenciaId,
    string TipoDocumentoCodigo,
    string Nome,
    bool Obrigatorio,
    string Situacao,
    string? OcorrenciaId,
    string? Consequencia,
    IReadOnlyList<ArquivoDoComprovante> Arquivos);

/// <summary>
/// Um arquivo enviado como o comprovante o registra: o SHA-256 do conteúdo, o tamanho e o formato.
/// Nunca o nome original nem a chave do armazenamento.
/// </summary>
/// <param name="Formato">O código do formato (<c>PDF</c>, <c>JPEG</c> ou <c>PNG</c>).</param>
public sealed record ArquivoDoComprovante(string HashSha256, long TamanhoBytes, string Formato);

/// <summary>Um arquivo confirmado da inscrição, ligado à exigência e à ocorrência de que é apresentação.</summary>
public sealed record ArquivoConfirmado(
    Guid ArquivoId,
    Guid ApresentacaoId,
    Guid ExigenciaId,
    string? OcorrenciaId,
    Enums.FormatoPermitido Formato,
    long TamanhoBytes,
    string HashSha256,
    DateTimeOffset ConfirmadoEm);
