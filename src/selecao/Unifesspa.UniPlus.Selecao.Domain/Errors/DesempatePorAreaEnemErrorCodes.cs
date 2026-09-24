namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do desempate pela nota de área do ENEM. Declarados como constantes, e não repetidos como literal no domínio, no
/// checklist de conformidade e no registro de erros da API: um código escrito duas vezes
/// diverge na primeira correção de grafia, e o item do checklist que o procura ficaria verde
/// em silêncio.
/// </summary>
public static class DesempatePorAreaEnemErrorCodes
{
    public const string AreasObrigatorias = "CriterioDesempate.AreasObrigatorias";
    public const string AreasEmExcesso = "CriterioDesempate.AreasEmExcesso";
    public const string AreaInvalida = "CriterioDesempate.AreaInvalida";
    public const string AreaRepetida = "CriterioDesempate.AreaRepetida";
    public const string AreaCitadaPorOutroCriterio = "ProcessoSeletivo.AreaEnemCitadaPorOutroCriterio";
    public const string SemQuadro = "ProcessoSeletivo.DesempateAreaEnemSemQuadro";
    public const string ForaDoQuadro = "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro";
}
