namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que um conjunto de fatos alcança nas regras da definição: seguindo as dependências declaradas
/// das derivações até os fatos que a definição pergunta, separa as derivações e os agregados
/// alcançados, os fatos perguntados de que eles dependem e, como <see cref="Pressupostos"/>, os que
/// a definição não pergunta nem calcula — os de outra fase e os do sistema.
/// </summary>
/// <remarks>
/// O agregado sobre um grupo depende do grupo inteiro, que é perguntado. O perguntado e o agregado são
/// folhas, a menos que se diga do que eles dependem para aparecer e ser exigidos: aí o fechamento
/// segue também esses fatos, até o ponto fixo.
/// </remarks>
public sealed record FechamentoDasDependencias(
    IReadOnlySet<string> Derivacoes,
    IReadOnlySet<string> Agregados,
    IReadOnlySet<string> Perguntados,
    IReadOnlyList<string> Pressupostos)
{
    /// <param name="citados">Os fatos de onde o fechamento parte.</param>
    /// <param name="perguntados">Os fatos que a definição pergunta, em item ou subitem.</param>
    /// <param name="derivacoes">As derivações que o fechamento pode atravessar, pelo fato derivado.</param>
    /// <param name="agregados">Os agregados que o fechamento pode alcançar, pelo fato agregado.</param>
    /// <param name="condicoes">
    /// Do que o perguntado ou o agregado alcançado depende para aparecer e ser exigido — a exibição e a
    /// obrigatoriedade do campo, ou do grupo, e a exibição da seção dele. Sem a entrada, é folha.
    /// </param>
    public static FechamentoDasDependencias De(
        IEnumerable<string> citados,
        IReadOnlySet<string> perguntados,
        IReadOnlyDictionary<string, RegrasDerivacaoFato> derivacoes,
        IReadOnlyDictionary<string, DefinicaoAgregado> agregados,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? condicoes = null)
    {
        ArgumentNullException.ThrowIfNull(citados);
        ArgumentNullException.ThrowIfNull(perguntados);
        ArgumentNullException.ThrowIfNull(derivacoes);
        ArgumentNullException.ThrowIfNull(agregados);

        HashSet<string> derivacoesAlcancadas = new(StringComparer.Ordinal);
        HashSet<string> agregadosAlcancados = new(StringComparer.Ordinal);
        HashSet<string> perguntadosAlcancados = new(StringComparer.Ordinal);
        HashSet<string> pressupostos = new(StringComparer.Ordinal);
        Queue<string> aVisitar = new(citados);
        HashSet<string> visitados = new(StringComparer.Ordinal);
        while (aVisitar.TryDequeue(out string? fato))
        {
            if (!visitados.Add(fato))
            {
                continue;
            }

            if (perguntados.Contains(fato))
            {
                perguntadosAlcancados.Add(fato);
                SeguirCondicoes(fato);
            }
            else if (derivacoes.TryGetValue(fato, out RegrasDerivacaoFato? derivacao))
            {
                derivacoesAlcancadas.Add(fato);
                foreach (string dependencia in derivacao.DependenciasDeclaradas)
                {
                    aVisitar.Enqueue(dependencia);
                }
            }
            else if (agregados.ContainsKey(fato))
            {
                agregadosAlcancados.Add(fato);
                SeguirCondicoes(fato);
            }
            else
            {
                pressupostos.Add(fato);
            }
        }

        return new(derivacoesAlcancadas, agregadosAlcancados, perguntadosAlcancados, [.. pressupostos.Order(StringComparer.Ordinal)]);

        void SeguirCondicoes(string fato)
        {
            foreach (string condicao in condicoes?.GetValueOrDefault(fato) ?? [])
            {
                aVisitar.Enqueue(condicao);
            }
        }
    }
}
