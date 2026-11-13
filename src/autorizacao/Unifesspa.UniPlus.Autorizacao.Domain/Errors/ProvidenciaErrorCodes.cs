namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da providência sobre concessão, declarados uma vez para que o domínio, os handlers e o
/// registro de erros da API não divirjam.
/// </summary>
public static class ProvidenciaErrorCodes
{
    public const string NaoEncontrada = "Providencia.NaoEncontrada";
    public const string TipoInvalido = "Providencia.TipoInvalido";
    public const string FundamentoInvalido = "Providencia.FundamentoInvalido";
    public const string MotivoInvalido = "Providencia.MotivoInvalido";
    public const string ConcessaoNaoAtiva = "Providencia.ConcessaoNaoAtiva";
    public const string JaHaAberta = "Providencia.JaHaAberta";
    public const string JaRespondida = "Providencia.JaRespondida";
    public const string JaAtendida = "Providencia.JaAtendida";
    public const string SolicitanteNaoResponde = "Providencia.SolicitanteNaoResponde";
    public const string BeneficiarioNaoResponde = "Providencia.BeneficiarioNaoResponde";
    public const string AutorNaoIdentificado = "Providencia.AutorNaoIdentificado";
    public const string SemPermissao = "Providencia.SemPermissao";
    public const string FiltroDeSituacaoInvalido = "Providencia.FiltroDeSituacaoInvalido";
    public const string ConflitoDeConcorrencia = "Providencia.ConflitoDeConcorrencia";
}
