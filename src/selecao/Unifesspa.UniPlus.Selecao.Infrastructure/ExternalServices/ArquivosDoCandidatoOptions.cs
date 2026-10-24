namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

/// <summary>
/// Os buckets de envio (quarentena) e definitivo dos arquivos do candidato (ADR-0146) e a credencial própria que os
/// acessa: a credencial geral do <c>Storage</c> enxerga todos os buckets, e a API não lê o de envio.
/// </summary>
public sealed class ArquivosDoCandidatoOptions
{
    public const string SectionName = "ArquivosDoCandidato";

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string BucketDeEnvio { get; init; } = string.Empty;

    public string BucketDefinitivo { get; init; } = string.Empty;
}
