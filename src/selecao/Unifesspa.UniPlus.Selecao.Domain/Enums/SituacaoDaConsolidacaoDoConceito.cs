namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Em que pé está a consolidação das atribuições dos corretores num conceito categórico.</summary>
public enum SituacaoDaConsolidacaoDoConceito
{
    /// <summary>Ainda faltam atribuições dos corretores exigidos.</summary>
    AguardandoCorrecao = 1,

    /// <summary>O critério foi atendido e o valor do conceito está definido.</summary>
    Consolidado = 2,

    /// <summary>Pelo critério de unanimidade, os corretores atribuíram valores diferentes.</summary>
    SemUnanimidade = 3,

    /// <summary>Pelos critérios de maioria, nenhum valor alcançou a maioria exigida.</summary>
    Empate = 4,
}
