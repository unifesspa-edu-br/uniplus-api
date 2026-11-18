namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

/// <summary>A operação humana na revisão que mudou o estado das concessões.</summary>
public enum OperacaoNaRevisaoDasConcessoes
{
    /// <summary>O envio de decisões da gestão de acessos.</summary>
    EnvioDaGestao = 1,

    /// <summary>A decisão de outra pessoa sobre a concessão retida.</summary>
    Retida = 2,

    /// <summary>A etapa da proteção de dados.</summary>
    EtapaDaProtecao = 3,

    /// <summary>O encerramento antes do prazo.</summary>
    Encerramento = 4,
}
