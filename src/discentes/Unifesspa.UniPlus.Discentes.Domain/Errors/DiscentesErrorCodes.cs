namespace Unifesspa.UniPlus.Discentes.Domain.Errors;

/// <summary>
/// Códigos dos erros de domínio do módulo Discentes.
/// </summary>
/// <remarks>
/// Existem como constantes, e não como texto solto no ponto de uso, para que o catálogo
/// de erros possa ser conferido contra o que o módulo declara — um código escrito
/// diretamente na chamada só se descobre lendo todo o código-fonte.
/// </remarks>
public static class DiscentesErrorCodes
{
    /// <summary>Erros do curso espelhado da origem.</summary>
    public static class Curso
    {
        public const string IdInvalido = "Curso.IdInvalido";
        public const string NomeVazio = "Curso.NomeVazio";
        public const string UnidadeIdInvalido = "Curso.UnidadeIdInvalido";
        public const string UnidadeNomeVazio = "Curso.UnidadeNomeVazio";
    }

    /// <summary>Erros do período de ingresso.</summary>
    public static class PeriodoIngresso
    {
        public const string AnoInvalido = "PeriodoIngresso.AnoInvalido";
        public const string PeriodoInvalido = "PeriodoIngresso.PeriodoInvalido";
    }

    /// <summary>Erros da situação acadêmica espelhada da origem.</summary>
    public static class SituacaoAcademica
    {
        public const string IdInvalido = "SituacaoAcademica.IdInvalido";
        public const string DescricaoVazia = "SituacaoAcademica.DescricaoVazia";
    }

    /// <summary>Erros do vínculo em si.</summary>
    public static class VinculoDiscente
    {
        public const string IdSigaaInvalido = "VinculoDiscente.IdSigaaInvalido";
        public const string MatriculaVazia = "VinculoDiscente.MatriculaVazia";
        public const string NomeVazio = "VinculoDiscente.NomeVazio";
        public const string NivelVazio = "VinculoDiscente.NivelVazio";
    }
}
