namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da <see cref="Entities.FaseCanonica"/> (UNI-REQ-0064),
/// prefixados por <c>FaseCanonica.</c>. Mapeados para status HTTP em
/// <c>ConfiguracaoDomainErrorRegistration</c>:
/// <list type="bullet">
///   <item><description><see cref="CodigoJaExiste"/> → 409 Conflict</description></item>
///   <item><description><see cref="NaoEncontrada"/> → 404 Not Found</description></item>
///   <item><description>demais → 422 Unprocessable Entity</description></item>
/// </list>
/// </summary>
public static class FaseCanonicaErrorCodes
{
    public const string CodigoObrigatorio = "FaseCanonica.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "FaseCanonica.CodigoFormatoInvalido";
    public const string CodigoForaDoConjuntoCanonico = "FaseCanonica.CodigoForaDoConjuntoCanonico";
    public const string CodigoJaExiste = "FaseCanonica.CodigoJaExiste";
    public const string NomeObrigatorio = "FaseCanonica.NomeObrigatorio";
    public const string NomeTamanho = "FaseCanonica.NomeTamanho";
    public const string DescricaoTamanho = "FaseCanonica.DescricaoTamanho";
    public const string DonoTipicoObrigatorio = "FaseCanonica.DonoTipicoObrigatorio";
    public const string DonoTipicoInvalido = "FaseCanonica.DonoTipicoInvalido";
    public const string AgrupaEtapasApenasAvaliacao = "FaseCanonica.AgrupaEtapasApenasAvaliacao";
    public const string ComplementacaoApenasFasesPermitidas = "FaseCanonica.ComplementacaoApenasFasesPermitidas";
    public const string BaseLegalTamanho = "FaseCanonica.BaseLegalTamanho";
    public const string NaoEncontrada = "FaseCanonica.NaoEncontrada";
    public const string OrigemDataObrigatoria = "FaseCanonica.OrigemDataObrigatoria";
    public const string OrigemDataInvalida = "FaseCanonica.OrigemDataInvalida";
    public const string ResultadoDefinitivoSemProduzirResultado = "FaseCanonica.ResultadoDefinitivoSemProduzirResultado";
}
