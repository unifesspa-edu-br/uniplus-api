namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// Coluna que tem de vir vazia: o separador final do arquivo do INEP produz um último campo sem
/// conteúdo, e a presença de texto ali indica um arquivo de outro leiaute.
/// </summary>
public sealed class LeitorDeColunaVazia : ILeitorDeColuna
{
    public string Codigo => "vazia";

    public IReadOnlyDictionary<string, string> Parametros { get; } = new Dictionary<string, string>();

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula) =>
        celula.IsEmpty ? LeituraDeCelula.Com(null) : LeituraDeCelula.Invalida(FalhaDeCelula.FormatoInvalido);
}
