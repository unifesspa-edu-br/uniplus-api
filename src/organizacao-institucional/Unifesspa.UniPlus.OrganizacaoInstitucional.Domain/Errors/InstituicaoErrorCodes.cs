namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

public static class InstituicaoErrorCodes
{
    public const string CodigoEmecObrigatorio = "Instituicao.CodigoEmecObrigatorio";
    public const string CodigoEmecTamanho = "Instituicao.CodigoEmecTamanho";
    public const string NomeObrigatorio = "Instituicao.NomeObrigatorio";
    public const string NomeTamanho = "Instituicao.NomeTamanho";
    public const string SiglaObrigatoria = "Instituicao.SiglaObrigatoria";
    public const string SiglaTamanho = "Instituicao.SiglaTamanho";
    public const string OrganizacaoAcademicaObrigatoria = "Instituicao.OrganizacaoAcademicaObrigatoria";
    public const string OrganizacaoAcademicaTamanho = "Instituicao.OrganizacaoAcademicaTamanho";
    public const string CategoriaAdministrativaObrigatoria = "Instituicao.CategoriaAdministrativaObrigatoria";
    public const string CategoriaAdministrativaTamanho = "Instituicao.CategoriaAdministrativaTamanho";
    public const string CampoOpcionalTamanho = "Instituicao.CampoOpcionalTamanho";

    /// <summary>
    /// Invariante singleton: já existe uma Instituição viva e a topologia de
    /// instância única (ADR-0055) admite no máximo uma.
    /// </summary>
    public const string JaExisteInstituicaoViva = "Instituicao.JaExisteInstituicaoViva";

    public const string NaoEncontrada = "Instituicao.NaoEncontrada";

    public const string UnidadeRaizNaoEncontrada = "Instituicao.UnidadeRaizNaoEncontrada";
    public const string UnidadeRaizNaoEhReitoria = "Instituicao.UnidadeRaizNaoEhReitoria";
}
