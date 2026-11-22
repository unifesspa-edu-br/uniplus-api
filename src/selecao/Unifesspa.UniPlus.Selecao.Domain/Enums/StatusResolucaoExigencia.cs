namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Situação de uma exigência documental para uma inscrição.</summary>
public enum StatusResolucaoExigencia
{
    Nenhuma = 0,
    Cumprida = 1,
    PendenteDeComprovacao = 2,
    NaoExigida = 3,
    AguardandoDados = 4,
}
