namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// Os itens de uma lista da classificação que a leitura adotou, cada um com a posição em que veio no documento, e as partes que ela
/// recusou. O item que cita uma etapa recusada pela leitura fica fora sem causa nova.
/// </summary>
internal sealed record ItensLidosDaClassificacao<T>(
    IReadOnlyList<(T Item, int PosicaoNoDocumento)> Itens,
    IReadOnlyList<ParteRecusada> Recusadas)
{
    private const string BlocoDaClassificacao = "classificacao";

    /// <summary>
    /// Lê cada item na ordem do documento. <paramref name="ler"/> recebe o item e quantos já foram adotados, devolve a causa em
    /// campos relativos ao bloco da classificação, nos nomes que <see cref="PonteiroNoDocumento"/> converte, e devolve nulo quando o
    /// item deve ficar fora sem causa nova.
    /// </summary>
    internal static ItensLidosDaClassificacao<T> De<TNoDocumento>(
        string lista,
        IReadOnlyList<TNoDocumento> doDocumento,
        Func<TNoDocumento, int> posicaoNoDocumento,
        Func<TNoDocumento, int, Result<T>?> ler)
    {
        List<(T Item, int PosicaoNoDocumento)> adotados = [];
        List<ParteRecusada> recusadas = [];
        foreach (TNoDocumento item in doDocumento)
        {
            Acumular(ler(item, adotados.Count), lista, posicaoNoDocumento(item), adotados, recusadas);
        }

        return new(adotados, recusadas);
    }

    private static void Acumular(
        Result<T>? lido, string lista, int posicao, List<(T Item, int PosicaoNoDocumento)> adotados, List<ParteRecusada> recusadas)
    {
        if (lido is { IsSuccess: true })
        {
            adotados.Add((lido.Value!, posicao));
        }
        else if (lido is not null)
        {
            recusadas.AddRange(lido.Errors.Select(causa => new ParteRecusada(lista, null, posicao, NoDocumento(causa))));
        }
    }

    /// <summary>A origem da lista: a posição de cada item adotado na ordem em que a declaração o entrega, que é a do documento.</summary>
    internal OrigemNoDocumento Origem(string lista) => new(
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal) { [lista] = [.. Itens.Select(static item => item.PosicaoNoDocumento)] },
        []);

    /// <summary>A causa com o campo no ponteiro do documento: o campo é relativo ao bloco da classificação.</summary>
    internal static FieldError NoDocumento(FieldError causa)
    {
        FieldError noBloco = causa.NoCaminhoDoItem(BlocoDaClassificacao);

        return noBloco with { Field = PonteiroNoDocumento.Converter(noBloco.Field, OrigemNoDocumento.Identidade) };
    }
}
