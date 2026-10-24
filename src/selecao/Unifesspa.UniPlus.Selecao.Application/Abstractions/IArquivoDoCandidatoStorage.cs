namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Os dois buckets dos arquivos do candidato (ADR-0146): o de envio (quarentena, PUT pré-assinado) e o
/// definitivo (só o que o verificador promoveu, GET pré-assinado). A API nunca lê o conteúdo e não remove nada do
/// bucket de envio, cuja expiração cuida do que ninguém verificou.
/// </summary>
public interface IArquivoDoCandidatoStorage
{
    Task<string> GerarUrlUploadAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default);

    /// <summary>URL de GET <c>inline</c> no bucket definitivo, com <paramref name="nomeDeApresentacao"/> como nome sugerido.</summary>
    Task<string> GerarUrlLeituraAsync(string chaveDefinitiva, string nomeDeApresentacao, TimeSpan expiracao, CancellationToken cancellationToken = default);

    /// <summary>Remove o objeto do bucket definitivo; o que já não existe não é erro.</summary>
    Task RemoverDefinitivoAsync(string chaveDefinitiva, CancellationToken cancellationToken = default);
}
