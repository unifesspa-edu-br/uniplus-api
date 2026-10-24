namespace Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O pedido de verificação que a API publica pelo outbox ao confirmar o envio. Fixa a versão a
/// verificar pelo <paramref name="ETag"/> que o PUT devolveu ao candidato.
/// </summary>
/// <param name="ArquivoId">O arquivo na API; volta no resultado.</param>
/// <param name="ChaveDeEnvio">A chave do objeto no bucket de envio (quarentena).</param>
/// <param name="ETag">O ETag da versão confirmada.</param>
/// <param name="ChaveDefinitiva">A chave em que o verificador promove o arquivo limpo.</param>
/// <param name="FormatoDeclarado">O formato que o candidato declarou ao enviar.</param>
/// <param name="TamanhoMaximoBytes">O máximo da exigência, nunca acima de 20 MB.</param>
public sealed record PedidoDeVerificacaoDeArquivo(
    Guid ArquivoId,
    string ChaveDeEnvio,
    string ETag,
    string ChaveDefinitiva,
    FormatoDoArquivo FormatoDeclarado,
    long TamanhoMaximoBytes);
