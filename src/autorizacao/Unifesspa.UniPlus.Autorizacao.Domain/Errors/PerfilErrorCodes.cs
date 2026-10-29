namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do perfil de acesso. Declarados como constantes, e não
/// repetidos como literal no domínio e no registro de erros da API: um código escrito duas
/// vezes diverge na primeira correção de grafia, e o mapeamento órfão só apareceria como
/// <c>500</c> na requisição que o disparasse.
/// </summary>
public static class PerfilErrorCodes
{
    public const string NomeInvalido = "Perfil.NomeInvalido";
    public const string DescricaoInvalida = "Perfil.DescricaoInvalida";
    public const string SemPermissao = "Perfil.SemPermissao";
    public const string PermissaoInexistente = "Perfil.PermissaoInexistente";
    public const string PermissaoReservada = "Perfil.PermissaoReservada";
    public const string AlcanceMisto = "Perfil.AlcanceMisto";
    public const string AlcanceAlterado = "Perfil.AlcanceAlterado";
    public const string AmpliacaoExigeEncarregada = "Perfil.AmpliacaoExigeEncarregada";
    public const string GovernancaImutavel = "Perfil.GovernancaImutavel";
    public const string NomeDuplicado = "Perfil.NomeDuplicado";
    public const string NaoEncontrado = "Perfil.NaoEncontrado";
    public const string IdDivergente = "Perfil.IdDivergente";
    public const string ConflitoDeConcorrencia = "Perfil.ConflitoDeConcorrencia";
}
