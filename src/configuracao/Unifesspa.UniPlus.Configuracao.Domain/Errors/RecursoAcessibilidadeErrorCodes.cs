namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do cadastro <c>RecursoAcessibilidade</c>. Mapeamento
/// esperado para HTTP (registrado em <c>ConfiguracaoDomainErrorRegistration</c>):
/// <list type="bullet">
///   <item><c>NomeJaExiste</c> → 409 Conflict.</item>
///   <item><c>NomeObrigatorio</c>, <c>NomeTamanho</c>, <c>DescricaoTamanho</c> → 422 Unprocessable Entity.</item>
///   <item><c>NaoEncontrado</c> → 404 Not Found.</item>
/// </list>
/// </summary>
public static class RecursoAcessibilidadeErrorCodes
{
    public const string NomeObrigatorio = "RecursoAcessibilidade.NomeObrigatorio";
    public const string NomeTamanho = "RecursoAcessibilidade.NomeTamanho";
    public const string NomeJaExiste = "RecursoAcessibilidade.NomeJaExiste";
    public const string DescricaoTamanho = "RecursoAcessibilidade.DescricaoTamanho";
    public const string NaoEncontrado = "RecursoAcessibilidade.NaoEncontrado";
}
