namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da revisão das concessões do perfil, declarados uma vez para que o domínio e o registro
/// de erros da API não divirjam.
/// </summary>
public static class RevisaoDasConcessoesErrorCodes
{
    public const string RevisaoNaoEncontrada = "RevisaoDasConcessoes.RevisaoNaoEncontrada";
    public const string RevisaoEncerrada = "RevisaoDasConcessoes.RevisaoEncerrada";
    public const string RevisaoEmEncerramento = "RevisaoDasConcessoes.RevisaoEmEncerramento";
    public const string PrazoEncerrado = "RevisaoDasConcessoes.PrazoEncerrado";
    public const string DecisoesJaEnviadas = "RevisaoDasConcessoes.DecisoesJaEnviadas";
    public const string DecisoesIncompletas = "RevisaoDasConcessoes.DecisoesIncompletas";
    public const string DecisoesInvalidas = "RevisaoDasConcessoes.DecisoesInvalidas";
    public const string DecisoesNaoEnviadas = "RevisaoDasConcessoes.DecisoesNaoEnviadas";
    public const string DecisaoDaPropriaConcessao = "RevisaoDasConcessoes.DecisaoDaPropriaConcessao";
    public const string MesmaPessoaNasDuasEtapas = "RevisaoDasConcessoes.MesmaPessoaNasDuasEtapas";
    public const string ConcessaoForaDaEtapa = "RevisaoDasConcessoes.ConcessaoForaDaEtapa";
    public const string ConcessaoJaDecidida = "RevisaoDasConcessoes.ConcessaoJaDecidida";
    public const string ExclusaoNaoConfirmada = "RevisaoDasConcessoes.ExclusaoNaoConfirmada";
    public const string MotivoInvalido = "RevisaoDasConcessoes.MotivoInvalido";
    public const string SemPermissaoNaEtapa = "RevisaoDasConcessoes.SemPermissaoNaEtapa";
}
