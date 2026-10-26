namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>Por que uma célula do arquivo não foi aceita pelo leitor da coluna.</summary>
public enum FalhaDeCelula
{
    FormatoInvalido = 1,

    /// <summary>O código de situação da prova não consta do dicionário do leiaute.</summary>
    CodigoDeSituacaoDesconhecido = 2,
}
