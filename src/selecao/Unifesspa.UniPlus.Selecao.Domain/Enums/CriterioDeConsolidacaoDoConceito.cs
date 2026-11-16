namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que basta para as atribuições dos corretores virarem o valor do conceito categórico da etapa. O
/// critério é declarado pela versão da regra de consolidação escolhida pelo edital; o código não fixa nenhum.
/// </summary>
public enum CriterioDeConsolidacaoDoConceito
{
    /// <summary>Todos os corretores atribuem o mesmo valor.</summary>
    Unanimidade = 1,

    /// <summary>Um valor tem mais atribuições que cada um dos outros.</summary>
    MaioriaSimples = 2,

    /// <summary>Um valor tem mais da metade das atribuições.</summary>
    MaioriaAbsoluta = 3,
}
