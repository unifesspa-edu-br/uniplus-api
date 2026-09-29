namespace Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Operações sobre o grafo de dependência entre fatos do candidato: cada nó cita os fatos de que
/// depende (ADR-0135). Funções puras sobre códigos, sem conhecer entidade de módulo — o cadastro
/// usa a detecção de ciclo para recusar a configuração, e o avaliador de formulário usa a ordenação
/// para resolver cada fato depois dos que ele cita.
/// </summary>
public static class GrafoDeFatos
{
    /// <summary>
    /// Busca em profundidade com marcação tricolor, devolvendo o caminho do primeiro ciclo
    /// encontrado — ou <see langword="null"/> quando o grafo é acíclico. A travessia segue, de cada
    /// nó, os fatos que ele <b>cita</b>, de modo que um caminho <c>A → B → A</c> se lê "A cita B,
    /// B cita A". Os nós são visitados na ordem de enumeração de <paramref name="citacoes"/>, o que
    /// torna o caminho reportado determinístico para a mesma entrada.
    /// </summary>
    /// <param name="citacoes">Os fatos citados por cada nó. Um fato citado que não é nó é folha.</param>
    public static IReadOnlyList<string>? DetectarCiclo(IReadOnlyDictionary<string, IReadOnlyCollection<string>> citacoes)
    {
        ArgumentNullException.ThrowIfNull(citacoes);

        HashSet<string> visitados = new(StringComparer.Ordinal);
        HashSet<string> naPilha = new(StringComparer.Ordinal);
        List<string> caminho = [];

        foreach (string codigo in citacoes.Keys)
        {
            if (Visitar(codigo) is { } ciclo)
            {
                return ciclo;
            }
        }

        return null;

        IReadOnlyList<string>? Visitar(string codigo)
        {
            if (naPilha.Contains(codigo))
            {
                int inicio = caminho.IndexOf(codigo);
                return [.. caminho[inicio..], codigo];
            }

            if (!visitados.Add(codigo) || !citacoes.TryGetValue(codigo, out IReadOnlyCollection<string>? citados))
            {
                return null;
            }

            naPilha.Add(codigo);
            caminho.Add(codigo);

            foreach (string citado in citados)
            {
                if (Visitar(citado) is { } ciclo)
                {
                    return ciclo;
                }
            }

            naPilha.Remove(codigo);
            caminho.RemoveAt(caminho.Count - 1);
            return null;
        }
    }

    /// <summary>
    /// Ordena os nós de modo que cada um venha depois de todos os nós que ele cita. Entre os nós
    /// prontos, vence a menor <c>Prioridade</c> e, no empate, a ordem de entrada — é o que mantém a
    /// ordem do formulário e deixa um fato derivado entrar assim que as suas dependências estão
    /// resolvidas.
    /// </summary>
    /// <param name="nos">
    /// Os nós, com os fatos que cada um cita e a prioridade. Um fato citado que não é nó é externo ao
    /// grafo (já conhecido, ou ausente) e não impõe ordem.
    /// </param>
    /// <returns>
    /// A ordem dos nós e os nós que ficaram de fora por pertencerem a um ciclo ou dependerem de um.
    /// </returns>
    public static OrdemTopologica Ordenar(IReadOnlyList<NoDoGrafo> nos)
    {
        ArgumentNullException.ThrowIfNull(nos);

        Dictionary<string, int> indice = new(StringComparer.Ordinal);
        for (int i = 0; i < nos.Count; i++)
        {
            if (!indice.TryAdd(nos[i].Codigo, i))
            {
                throw new ArgumentException($"O nó '{nos[i].Codigo}' aparece mais de uma vez no grafo.", nameof(nos));
            }
        }

        int[] pendentes = new int[nos.Count];
        List<int>[] dependentes = [.. nos.Select(static _ => new List<int>())];
        for (int i = 0; i < nos.Count; i++)
        {
            foreach (string citado in nos[i].Citados.Distinct(StringComparer.Ordinal))
            {
                if (!indice.TryGetValue(citado, out int dependencia))
                {
                    continue;
                }

                // Um nó que cita a si mesmo conta a pendência sem ninguém para liberá-la: nunca fica
                // pronto, como qualquer ciclo.
                pendentes[i]++;
                if (dependencia != i)
                {
                    dependentes[dependencia].Add(i);
                }
            }
        }

        PriorityQueue<int, (int Prioridade, int Entrada)> prontos = new();
        for (int i = 0; i < nos.Count; i++)
        {
            if (pendentes[i] == 0)
            {
                prontos.Enqueue(i, (nos[i].Prioridade, i));
            }
        }

        List<string> ordem = [];
        while (prontos.TryDequeue(out int atual, out _))
        {
            ordem.Add(nos[atual].Codigo);
            foreach (int dependente in dependentes[atual])
            {
                if (--pendentes[dependente] == 0)
                {
                    prontos.Enqueue(dependente, (nos[dependente].Prioridade, dependente));
                }
            }
        }

        HashSet<string> ordenados = new(ordem, StringComparer.Ordinal);
        return new OrdemTopologica(ordem, [.. nos.Select(static n => n.Codigo).Where(c => !ordenados.Contains(c))]);
    }
}

/// <summary>Um nó do grafo de fatos: o código, os fatos que ele cita e a prioridade de ordenação.</summary>
public sealed record NoDoGrafo(string Codigo, IReadOnlyCollection<string> Citados, int Prioridade);

/// <summary>
/// Resultado de <see cref="GrafoDeFatos.Ordenar"/>: os nós em ordem e os que não puderam ser
/// ordenados porque pertencem a um ciclo ou dependem de um.
/// </summary>
public sealed record OrdemTopologica(IReadOnlyList<string> Ordem, IReadOnlyList<string> ForaDeOrdem);
