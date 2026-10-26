namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// O sexo como o INEP o grafa: <c>F</c> ou <c>M</c>. Célula vazia dá valor nulo; qualquer outro
/// texto indica um arquivo de outro leiaute.
/// </summary>
public sealed class LeitorDeSexo : ILeitorDeColuna
{
    public string Codigo => "sexo";

    public IReadOnlyDictionary<string, string> Parametros { get; } = new Dictionary<string, string>
    {
        ["valores"] = "F,M",
    };

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula) => celula switch
    {
        [] => LeituraDeCelula.Com(null),
        "F" => LeituraDeCelula.Com("F"),
        "M" => LeituraDeCelula.Com("M"),
        _ => LeituraDeCelula.Invalida(FalhaDeCelula.FormatoInvalido),
    };
}
