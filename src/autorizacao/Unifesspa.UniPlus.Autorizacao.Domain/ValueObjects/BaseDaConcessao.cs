namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A base de uma permissão numa concessão: a classificação e o escopo contra os quais o catálogo atual se mede. O catálogo
/// que sobe a classificação acima da base, ou que muda o escopo, suspende a permissão na concessão.
/// </summary>
public sealed record BaseDaConcessao(ClassificacaoDeProtecao Classificacao, bool DeUnidade)
{
    /// <summary>O catálogo tem classificação maior ou escopo diferente do da base.</summary>
    public bool SuspendeFrenteA(ClassificacaoDeProtecao classificacaoDoCatalogo, bool deUnidadeDoCatalogo) =>
        classificacaoDoCatalogo > Classificacao || deUnidadeDoCatalogo != DeUnidade;
}
