namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// Lê o valor de uma coluna de um formato. Um leiaute novo é composto de leitores que já existem;
/// só um formato que nenhum cobre pede um leitor novo.
/// </summary>
public interface ILeitorDeColuna
{
    /// <summary>O nome do formato, que entra na identidade do leiaute.</summary>
    string Codigo { get; }

    /// <summary>Os parâmetros do formato que mudam o que o leitor aceita; entram na identidade do leiaute.</summary>
    IReadOnlyDictionary<string, string> Parametros { get; }

    LeituraDeCelula Ler(ReadOnlySpan<char> celula);
}
