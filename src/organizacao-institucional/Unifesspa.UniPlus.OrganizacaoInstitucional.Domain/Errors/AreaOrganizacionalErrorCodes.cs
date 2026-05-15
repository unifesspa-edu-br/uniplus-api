namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Códigos canônicos de erros de domínio de <c>AreaOrganizacional</c>.
/// Mapeados para wire codes <c>uniplus.area_organizacional.*</c> em
/// <c>OrganizacaoDomainErrorRegistration</c> (ADR-0024/0023).
/// </summary>
public static class AreaOrganizacionalErrorCodes
{
    public const string NomeObrigatorio = "AreaOrganizacional.NomeObrigatorio";
    public const string NomeTamanho = "AreaOrganizacional.NomeTamanho";
    public const string DescricaoObrigatoria = "AreaOrganizacional.DescricaoObrigatoria";
    public const string DescricaoTamanho = "AreaOrganizacional.DescricaoTamanho";
    public const string AdrReferenceObrigatorio = "AreaOrganizacional.AdrReferenceObrigatorio";
    public const string AdrReferenceFormatoInvalido = "AreaOrganizacional.AdrReferenceFormatoInvalido";
    public const string TipoInvalido = "AreaOrganizacional.TipoInvalido";
    public const string CodigoJaExiste = "AreaOrganizacional.CodigoJaExiste";
    public const string NaoEncontrada = "AreaOrganizacional.NaoEncontrada";
}
