namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Categorias de <c>ObrigatoriedadeLegal</c> per ADR-0058 §"Forma de
/// ObrigatoriedadeLegal" — agrupam as regras pela dimensão do edital que
/// avaliam (etapa, modalidade, documento, bônus, atendimento, desempate)
/// para suportar filtros admin e dashboards de conformidade.
/// </summary>
/// <remarks>
/// O sentinel <see cref="Nenhuma"/> garante que <c>default(CategoriaObrigatoriedade)</c>
/// nunca colida com categoria real — o avaliador e a factory rejeitam-no
/// explicitamente. Novas categorias entram via amendment do ADR-0058 e
/// adição de variante tipada correspondente em
/// <c>PredicadoObrigatoriedade</c> quando for o caso.
/// </remarks>
public enum CategoriaObrigatoriedade
{
    Nenhuma = 0,
    Etapa = 1,
    Modalidade = 2,
    Desempate = 3,
    Documento = 4,
    Bonus = 5,
    Atendimento = 6,
    Outros = 7,
}
