namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>O número de inscrição do INEP: doze dígitos.</summary>
public sealed class LeitorDeNumeroDeInscricaoNoEnem : ILeitorDeColuna
{
    public const int QuantidadeDeDigitos = 12;

    public string Codigo => "numeroDeInscricaoNoEnem";

    public IReadOnlyDictionary<string, string> Parametros { get; } = new Dictionary<string, string>();

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula) =>
        celula.Length == QuantidadeDeDigitos && LeitorDeCpf.SomenteDigitos(celula)
            ? LeituraDeCelula.Com(celula.ToString())
            : LeituraDeCelula.Invalida(FalhaDeCelula.FormatoInvalido);
}
