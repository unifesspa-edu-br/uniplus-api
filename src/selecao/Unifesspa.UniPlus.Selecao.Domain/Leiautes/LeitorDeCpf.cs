namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// Onze dígitos. O dígito verificador não é conferido: um CPF inválido simplesmente não casa com
/// nenhuma inscrição e vira ocorrência da linha, em vez de recusar o arquivo inteiro.
/// </summary>
public sealed class LeitorDeCpf : ILeitorDeColuna
{
    private const int QuantidadeDeDigitos = 11;

    public string Codigo => "cpf";

    public IReadOnlyDictionary<string, string> Parametros { get; } = new Dictionary<string, string>();

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula) =>
        celula.Length == QuantidadeDeDigitos && SomenteDigitos(celula)
            ? LeituraDeCelula.Com(celula.ToString())
            : LeituraDeCelula.Invalida(FalhaDeCelula.FormatoInvalido);

    internal static bool SomenteDigitos(ReadOnlySpan<char> texto)
    {
        foreach (char c in texto)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
