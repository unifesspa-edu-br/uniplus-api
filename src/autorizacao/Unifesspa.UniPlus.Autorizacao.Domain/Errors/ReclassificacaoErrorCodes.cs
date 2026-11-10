namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da revisão das permissões reclassificadas, declarados uma vez para que o domínio e o
/// registro de erros da API não divirjam.
/// </summary>
public static class ReclassificacaoErrorCodes
{
    public const string NaoEncontrada = "Reclassificacao.NaoEncontrada";
    public const string Superada = "Reclassificacao.Superada";
    public const string EtapaJaDecidida = "Reclassificacao.EtapaJaDecidida";
    public const string ConcessaoForaDaReclassificacao = "Reclassificacao.ConcessaoForaDaReclassificacao";
    public const string EtapaNaoExigida = "Reclassificacao.EtapaNaoExigida";
    public const string DecisorEBeneficiario = "Reclassificacao.DecisorEBeneficiario";
    public const string MesmoDecisorNasDuasEtapas = "Reclassificacao.MesmoDecisorNasDuasEtapas";
    public const string DocumentoNaoLido = "Reclassificacao.DocumentoNaoLido";
    public const string EscopoNaoComporta = "Reclassificacao.EscopoNaoComporta";
    public const string MotivoInvalido = "Reclassificacao.MotivoInvalido";
    public const string SemPermissaoNaEtapa = "Reclassificacao.SemPermissaoNaEtapa";
    public const string EtapaInvalida = "Reclassificacao.EtapaInvalida";
    public const string ResultadoInvalido = "Reclassificacao.ResultadoInvalido";
    public const string AlvoInvalido = "Reclassificacao.AlvoInvalido";
}
