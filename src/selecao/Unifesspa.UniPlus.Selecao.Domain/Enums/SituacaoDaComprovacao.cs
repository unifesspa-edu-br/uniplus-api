namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Situação do cumprimento de uma regra de comprovação.</summary>
public enum SituacaoDaComprovacao
{
    Nenhum = 0,
    Cumprida = 1,
    PendenteDeComprovacao = 2,
    AguardandoDados = 3,
    NaoExigida = 4,
    SemPossibilidadeDeCumprimento = 5,
}
