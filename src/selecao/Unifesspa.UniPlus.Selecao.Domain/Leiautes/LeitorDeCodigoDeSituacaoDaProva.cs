namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Traduz o código de situação da prova pelo dicionário do leiaute. Um código fora do dicionário
/// recusa o arquivo: aceitá-lo poderia lançar a nota de quem não fez a prova.
/// </summary>
public sealed class LeitorDeCodigoDeSituacaoDaProva : ILeitorDeColuna
{
    private readonly FrozenDictionary<string, SituacaoDaProvaNoEnem> _dicionario;

    public LeitorDeCodigoDeSituacaoDaProva(IReadOnlyDictionary<string, SituacaoDaProvaNoEnem> dicionario)
    {
        ArgumentNullException.ThrowIfNull(dicionario);

        _dicionario = dicionario.ToFrozenDictionary(StringComparer.Ordinal);
        Parametros = dicionario.ToDictionary(static p => p.Key, static p => p.Value.ToString(), StringComparer.Ordinal);
    }

    public string Codigo => "codigoDeSituacaoDaProva";

    public IReadOnlyDictionary<string, string> Parametros { get; }

    public LeituraDeCelula Ler(ReadOnlySpan<char> celula)
    {
        FrozenDictionary<string, SituacaoDaProvaNoEnem>.AlternateLookup<ReadOnlySpan<char>> busca =
            _dicionario.GetAlternateLookup<ReadOnlySpan<char>>();

        return busca.TryGetValue(celula, out SituacaoDaProvaNoEnem situacao)
            ? LeituraDeCelula.Com(situacao)
            : LeituraDeCelula.Invalida(FalhaDeCelula.CodigoDeSituacaoDesconhecido);
    }
}
