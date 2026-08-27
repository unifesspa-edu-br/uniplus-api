namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Acesso de leitura a um documento do Edital confirmado — emitido a cada
/// pedido, e não guardado em lugar algum.
/// <para>
/// A URL é credencial de acesso ao objeto: quem a tem lê o arquivo, sem
/// passar de novo por autenticação nem por autorização. É por isso que ela
/// não acompanha a listagem de documentos, onde seria distribuída por
/// documento a cada consulta e com o prazo correndo desde então, para links
/// que talvez ninguém abra. Aqui o prazo começa quando o acesso é de fato
/// pedido.
/// </para>
/// </summary>
/// <param name="Url">URL pre-assinada de leitura, válida até <paramref name="ExpiraEm"/>.</param>
/// <param name="ExpiraEm">
/// Fim da validade da assinatura. Serve para o cliente saber que o link
/// envelhece — não para ele guardar a URL até lá.
/// </param>
public sealed record AcessoDocumentoEditalDto(
    Uri Url,
    DateTimeOffset ExpiraEm);
