namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// O bucket de envio do PDF de designação (ADR-0146): o cliente sobe o arquivo por uma URL de PUT
/// assinada e a API nunca recebe os bytes; quem confere e promove o arquivo é o verificador.
/// </summary>
public interface IArmazenamentoDoDocumentoDaInstalacao
{
    Task<string> GerarUrlDeEnvioAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default);
}
