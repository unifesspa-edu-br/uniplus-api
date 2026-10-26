namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using System.Globalization;

/// <summary>
/// A nota de uma área: ponto como separador decimal, no máximo uma casa, de 0 a 1000. Vírgula,
/// duas ou mais casas, sinal e espaços são recusados — um formato diferente do esperado indica que
/// o arquivo é de outro leiaute, e aceitá-lo gravaria uma nota errada.
/// </summary>
public sealed class LeitorDeNotaDoEnem : ILeitorDeColuna
{
    private const decimal NotaMaxima = 1000m;

    public string Codigo => "notaDoEnem";

    public IReadOnlyDictionary<string, string> Parametros { get; } = new Dictionary<string, string>
    {
        ["casasDecimais"] = "1",
        ["maximo"] = "1000",
    };

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula)
    {
        int ponto = celula.IndexOf('.');
        ReadOnlySpan<char> inteira = ponto < 0 ? celula : celula[..ponto];
        ReadOnlySpan<char> fracao = ponto < 0 ? default : celula[(ponto + 1)..];

        bool formatoValido = inteira.Length is >= 1 and <= 4
            && LeitorDeCpf.SomenteDigitos(inteira)
            && (ponto < 0 || (fracao.Length == 1 && LeitorDeCpf.SomenteDigitos(fracao)));
        if (!formatoValido)
        {
            return LeituraDeCelula.Invalida(FalhaDeCelula.FormatoInvalido);
        }

        decimal nota = decimal.Parse(celula, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

        return nota <= NotaMaxima
            ? LeituraDeCelula.Com(nota)
            : LeituraDeCelula.Invalida(FalhaDeCelula.FormatoInvalido);
    }
}
