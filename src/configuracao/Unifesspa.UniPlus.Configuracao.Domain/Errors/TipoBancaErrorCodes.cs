namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do <see cref="Entities.TipoBanca"/> (UNI-REQ-0064),
/// prefixados por <c>TipoBanca.</c>. Mapeados para status HTTP em
/// <c>ConfiguracaoDomainErrorRegistration</c>:
/// <list type="bullet">
///   <item><description><see cref="CodigoJaExiste"/> → 409 Conflict</description></item>
///   <item><description><see cref="NaoEncontrado"/> → 404 Not Found</description></item>
///   <item><description>demais → 422 Unprocessable Entity</description></item>
/// </list>
/// </summary>
public static class TipoBancaErrorCodes
{
    public const string CodigoObrigatorio = "TipoBanca.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "TipoBanca.CodigoFormatoInvalido";
    public const string CodigoForaDoConjuntoCanonico = "TipoBanca.CodigoForaDoConjuntoCanonico";
    public const string CodigoJaExiste = "TipoBanca.CodigoJaExiste";
    public const string NomeObrigatorio = "TipoBanca.NomeObrigatorio";
    public const string NomeTamanho = "TipoBanca.NomeTamanho";
    public const string FaseTipicaTamanho = "TipoBanca.FaseTipicaTamanho";
    public const string DescricaoTamanho = "TipoBanca.DescricaoTamanho";
    public const string NaoEncontrado = "TipoBanca.NaoEncontrado";
}
