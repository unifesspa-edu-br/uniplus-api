namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ReconciliacaoDosNiveisDeAtendimento
{
    /// <summary>Os níveis como o documento os declarou: a chave declarada se conserva, e a ordem é a posição mais 1.</summary>
    internal static IEnumerable<NivelDeAtendimento> Conservar(IReadOnlyList<NivelDeAtendimentoDeclarado?>? declarados) =>
        (declarados ?? []).Select(static (nivel, posicao) => Conferido(nivel!) with { Ordem = posicao + 1 });

    internal static List<NivelDeAtendimento> Reconciliar(
        IEnumerable<NivelDeAtendimento> existentes, IReadOnlyList<NivelDeAtendimentoDeclarado?>? declarados)
    {
        Dictionary<ChaveLocal, NivelDeAtendimento> restantes = existentes.ToDictionary(static nivel => nivel.Chave);
        List<NivelDeAtendimento> lista = [.. (declarados ?? []).Select(static nivel => Conferido(nivel!))];
        ChavesEmUso reservadas = new();
        foreach (NivelDeAtendimento nivel in lista.Where(nivel => restantes.ContainsKey(nivel.Chave)))
        {
            reservadas.Adicionar(nivel.Chave);
        }

        return [.. lista.Select((nivel, posicao) => Preparar(nivel, posicao, restantes, reservadas))];
    }

    // A escala já passou pela conferência da forma: o rótulo e a fração estão informados; a chave inválida fica sem valor,
    // e a chave nova é formada a partir do rótulo.
    private static NivelDeAtendimento Conferido(NivelDeAtendimentoDeclarado declarado) =>
        new(declarado.Rotulo!, declarado.Fracao!.Value) { Chave = ChaveLocal.Criar(declarado.Chave).Value };

    private static NivelDeAtendimento Preparar(
        NivelDeAtendimento declarado, int posicao, Dictionary<ChaveLocal, NivelDeAtendimento> restantes, ChavesEmUso reservadas)
    {
        if (restantes.Remove(declarado.Chave, out NivelDeAtendimento? existente))
        {
            existente.Rotulo = declarado.Rotulo.Trim();
            existente.Fracao = declarado.Fracao;
            existente.Ordem = posicao + 1;
            return existente;
        }

        ChaveLocal chave = FormacaoDeChaveLocal.Formar("nivel", declarado.Rotulo, reservadas);
        reservadas.Adicionar(chave);
        return declarado with { Chave = chave, Rotulo = declarado.Rotulo.Trim(), Ordem = posicao + 1 };
    }
}
