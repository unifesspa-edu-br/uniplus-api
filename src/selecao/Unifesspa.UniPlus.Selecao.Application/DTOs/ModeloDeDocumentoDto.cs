namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resposta do início do envio do modelo de documento: o id do modelo, a URL de PUT direto ao
/// storage, o <c>Content-Type</c> que o PUT envia e o fim da validade da URL.
/// </summary>
public sealed record IniciarEnvioDoModeloDeDocumentoDto(
    Guid ModeloDeDocumentoId,
    Uri UrlUpload,
    string ContentTypeExigido,
    DateTimeOffset ExpiraEm);

/// <summary>
/// O modelo de documento como o administrador o enxerga — resposta da confirmação. Nada aqui
/// endereça o storage: a URL de PUT ainda vale até expirar, e quem a conhece sobrescreveria o
/// objeto do pendente.
/// </summary>
/// <param name="Formato"><c>DOCX</c> ou <c>ODT</c>.</param>
/// <param name="Status"><c>Pendente</c> ou <c>Confirmado</c>.</param>
/// <param name="TamanhoBytes">Nulo enquanto pendente.</param>
/// <param name="HashSha256">Nulo enquanto pendente — calculado no servidor na confirmação.</param>
/// <param name="ConfirmadoEm">Nulo enquanto pendente.</param>
public sealed record ModeloDeDocumentoDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    string NomeArquivo,
    string Formato,
    string Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset ExpiraEm,
    long? TamanhoBytes,
    string? HashSha256,
    DateTimeOffset? ConfirmadoEm);

/// <summary>
/// Acesso de leitura a um modelo confirmado, emitido a cada pedido. A URL é credencial de acesso
/// ao objeto, válida até <paramref name="ExpiraEm"/>.
/// </summary>
public sealed record AcessoModeloDeDocumentoDto(Uri Url, DateTimeOffset ExpiraEm);
