namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>
/// Corpo do <c>POST /api/autorizacao/perfis</c>: o que a versão
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

/// <summary>
/// Corpo do <c>POST /api/autorizacao/perfis/{id}/versoes</c>: o que a versão declara, o tamanho previsto do documento que
/// a institui e, quando ela retira permissões da versão em vigor, o motivo obrigatório e a categoria opcional da remoção.
/// </summary>
/// <param name="Protocolo">Número do processo ou protocolo do documento instituidor.</param>
/// <param name="TamanhoDoDocumento">Tamanho do PDF, em bytes: de 1 byte a 10 MB.</param>
/// <param name="MotivoDaRemocao">Motivo da remoção, de até 500 caracteres; obrigatório quando a proposta retira permissão.</param>
/// <param name="CategoriaDaRemocao">Categoria da remoção, entre as previstas.</param>
public sealed record ProporVersaoRequest(
    string? Nome,
    string? Descricao,
    IReadOnlyList<string>? Permissoes,
    string? Protocolo,
    long TamanhoDoDocumento,
    string? MotivoDaRemocao,
    string? CategoriaDaRemocao);

/// <summary>
/// O que a proposta de versão produziu: o envio do documento quando ela criou uma versão pendente ou, quando só retirou
/// permissões, o perfil com a remoção já em vigor.
/// </summary>
/// <param name="Envio">A URL de PUT do documento da versão pendente; nula quando a proposta só retirou permissões.</param>
/// <param name="Perfil">O perfil depois da remoção; nulo quando a proposta criou uma versão pendente.</param>
public sealed record ResultadoDaPropostaDto(EnvioDoDocumentoDto? Envio, PerfilDto? Perfil);

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
