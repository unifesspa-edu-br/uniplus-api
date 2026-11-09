namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>
/// Corpo do <c>POST /api/autorizacao/perfis</c> e do <c>POST /api/autorizacao/perfis/{id}/versoes</c>: o que a versão
/// declara e o tamanho previsto do documento que a institui.
/// </summary>
/// <param name="Protocolo">Número do processo ou protocolo do documento instituidor.</param>
/// <param name="TamanhoDoDocumento">Tamanho do PDF, em bytes: de 1 byte a 10 MB.</param>
public sealed record DadosDaVersaoRequest(
    string? Nome,
    string? Descricao,
    IReadOnlyList<string>? Permissoes,
    string? Protocolo,
    long TamanhoDoDocumento);

/// <summary>Corpo do <c>POST /api/autorizacao/perfis/{id}/versoes/{versaoId}/documento</c>.</summary>
public sealed record EnviarDocumentoDaVersaoRequest(long TamanhoDoDocumento);

/// <summary>Corpo da confirmação do envio do documento da versão.</summary>
public sealed record ConfirmarDocumentoDaVersaoRequest(string? Etag);

/// <summary>Resposta que entrega a URL de PUT assinada do documento de uma versão.</summary>
/// <param name="UrlDeEnvio">URL de PUT assinada; credencial de acesso, de validade curta.</param>
/// <param name="TipoDeMidia">O <c>Content-Type</c> que o PUT precisa enviar.</param>
public sealed record EnvioDoDocumentoDto(
    Guid PerfilId,
    Guid VersaoId,
    Guid DocumentoId,
    string Situacao,
    Uri UrlDeEnvio,
    string TipoDeMidia,
    DateTimeOffset ExpiraEm);

/// <summary>Documento instituidor da versão depois da confirmação do envio.</summary>
public sealed record DocumentoDaVersaoDto(Guid Id, Guid VersaoId, string Situacao, long TamanhoDeclarado, DateTimeOffset? EnviadoEm);

/// <summary>URL assinada de leitura de um documento verificado, de validade curta.</summary>
public sealed record LeituraDoDocumentoDaVersaoDto(Uri Url, DateTimeOffset ExpiraEm);
