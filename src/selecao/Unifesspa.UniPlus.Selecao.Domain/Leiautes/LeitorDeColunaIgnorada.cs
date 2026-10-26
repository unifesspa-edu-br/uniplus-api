namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// Coluna que o Uni+ não usa: não é lida, não é validada e não é gravada. Mesmo sendo dado do
/// candidato (nome, data de nascimento), o que a avaliação não usa nem a conferência com o INEP
/// exige não entra no sistema.
/// </summary>
public sealed class LeitorDeColunaIgnorada : ILeitorDeColuna
{
    public string Codigo => "ignorada";

    public IReadOnlyDictionary<string, string> Parametros { get; } = new Dictionary<string, string>();

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula) => LeituraDeCelula.Com(null);
}
