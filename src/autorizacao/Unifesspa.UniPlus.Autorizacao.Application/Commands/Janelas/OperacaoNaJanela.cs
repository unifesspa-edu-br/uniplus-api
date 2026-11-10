namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

/// <summary>A operação humana sobre a janela de migração que mudou o estado das concessões.</summary>
public enum OperacaoNaJanela
{
    /// <summary>O lote da gestão de acessos.</summary>
    Lote = 1,

    /// <summary>A decisão de outra pessoa sobre a concessão retida.</summary>
    Retida = 2,

    /// <summary>A etapa da proteção de dados.</summary>
    EtapaDaProtecao = 3,

    /// <summary>O encerramento antes do prazo.</summary>
    Encerramento = 4,
}
