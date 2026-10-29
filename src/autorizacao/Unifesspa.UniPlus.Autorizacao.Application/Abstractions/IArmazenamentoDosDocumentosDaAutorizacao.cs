namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// O armazenamento dos PDFs da Autorização (ADR-0146): o cliente sobe o arquivo por uma URL de PUT assinada no
/// bucket de envio e a API nunca recebe os bytes; quem confere e promove o arquivo é o verificador. A leitura
/// é sempre do objeto promovido, por URL assinada de validade curta.
/// </summary>
public interface IArmazenamentoDosDocumentosDaAutorizacao
{
    Task<string> GerarUrlDeEnvioAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default);

    /// <summary>URL de GET do objeto promovido ao bucket definitivo, que o navegador baixa com o nome informado.</summary>
    Task<string> GerarUrlDeLeituraAsync(string chaveDefinitiva, string nomeDoArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default);
}
