namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro do value object <c>GrupoCurso</c>. Os agregados que o
/// consomem hoje traduzem a falha para o código do próprio campo
/// (<c>PesoAreaEnem.GrupoCursoInvalido</c>, <c>Curso.GrupoAreaEnemInvalido</c>),
/// mas o código continua registrado para que a propagação direta responda
/// 422 em vez do 500 genérico do mapper.
/// </summary>
public static class GrupoCursoErrorCodes
{
    public const string ForaDoDominio = "GrupoCurso.ForaDoDominio";
}
