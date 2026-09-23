namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro do value object <c>AreaEnem</c>. Registrados no mapper para
/// que a falha propagada por quem consumir o vocabulário responda 422, e não o
/// 500 genérico de código sem mapeamento.
/// </summary>
public static class AreaEnemErrorCodes
{
    public const string ForaDoDominio = "AreaEnem.ForaDoDominio";
}
