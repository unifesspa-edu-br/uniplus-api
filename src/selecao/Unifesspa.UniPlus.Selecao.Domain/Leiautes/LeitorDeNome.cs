namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// O nome do candidato como o arquivo o traz, sem espaços nas pontas. Célula vazia dá nome nulo, e o
/// que passa de <see cref="TamanhoMaximo"/> caracteres é cortado: o nome só identifica a linha na
/// conferência, não participa da avaliação.
/// </summary>
public sealed class LeitorDeNome : ILeitorDeColuna
{
    public const int TamanhoMaximo = 200;

    public string Codigo => "nome";

    public IReadOnlyDictionary<string, string> Parametros { get; } = new Dictionary<string, string>
    {
        ["tamanhoMaximo"] = TamanhoMaximo.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula)
    {
        ReadOnlySpan<char> nome = celula.Trim();
        if (nome.IsEmpty)
        {
            return LeituraDeCelula.Com(null);
        }

        return LeituraDeCelula.Com(nome[..Math.Min(nome.Length, TamanhoMaximo)].ToString());
    }
}
