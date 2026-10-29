namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

/// <summary>
/// Os buckets e a credencial do PDF de designação: os mesmos dos arquivos do candidato (<c>ArquivosDoCandidato</c>),
/// porque o verificador e a cifra do armazenamento são os mesmos (ADR-0146). A credencial geral do <c>Storage</c>
/// enxerga todos os buckets, e a API não lê o de envio.
/// </summary>
public sealed class ArquivosDaInstalacaoOptions
{
    public const string SectionName = "ArquivosDoCandidato";

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string BucketDeEnvio { get; init; } = string.Empty;
}
