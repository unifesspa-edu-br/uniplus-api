namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Lê um cadastro externo uma única vez por chave dentro da execução de um comando. A ausência também é
/// lembrada: o cadastro é consultado com o <c>SELECT ... FOR UPDATE</c> do processo na mão, e reler a mesma
/// chave por item de cada fase ou etapa serializaria quem edita o mesmo processo por todo esse tempo.
/// </summary>
/// <typeparam name="TChave">Chave de consulta no cadastro.</typeparam>
/// <typeparam name="TValor">Dado bruto do cadastro; a leitura não guarda instância de entidade.</typeparam>
internal sealed class LeituraMemoizada<TChave, TValor>(
    Func<TChave, CancellationToken, Task<TValor?>> ler,
    IEqualityComparer<TChave>? comparador = null)
    where TChave : notnull
    where TValor : class
{
    private readonly Dictionary<TChave, TValor?> _lidos = new(comparador);

    /// <summary>O valor da chave, lido do cadastro na primeira consulta; <c>null</c> quando o cadastro não o tem.</summary>
    public async Task<TValor?> ObterAsync(TChave chave, CancellationToken cancellationToken)
    {
        if (_lidos.TryGetValue(chave, out TValor? memoizado))
        {
            return memoizado;
        }

        TValor? lido = await ler(chave, cancellationToken).ConfigureAwait(false);
        _lidos[chave] = lido;
        return lido;
    }
}
