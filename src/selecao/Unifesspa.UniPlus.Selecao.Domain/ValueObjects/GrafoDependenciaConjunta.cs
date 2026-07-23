namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O grafo de dependência conjunto de um processo (Story #928, §6): torna <b>explícito</b> o que a
/// configuração hoje expressa implicitamente (ordem de coleta, pré-condições, dependências de
/// derivação e gatilhos). Os nós são campos, fatos e exigências; as quatro classes de aresta são
/// <see cref="TipoArestaGrafo"/>. O grafo SHALL ser acíclico (DAG) considerando as quatro classes
/// <b>juntas</b>, e a sua ordenação topológica é a ordem de coleta.
/// </summary>
/// <remarks>
/// <para>
/// Toda aresta aponta do produtor para o consumidor, então "produtor antes do consumidor" é
/// exatamente "acíclico". A aresta de <see cref="TipoArestaGrafo.Producao"/> (<c>campo → fato</c>) é
/// o que impede uma ordenação de pôr o fato antes do campo que o produz — sem ela, campo e fato do
/// mesmo código não teriam ordem relativa.
/// </para>
/// <para>
/// Função pura de construção, sem tocar banco nem relógio: a mesma configuração produz o mesmo
/// grafo, a mesma ordem e o mesmo veredicto de aciclicidade. A identidade canônica dos nós
/// (<c>tipoDeNo/escopo/codigo</c> UTF-8 NFC) e o congelamento com hash são da fatia de determinismo
/// (§7); aqui a identidade de runtime é o par <c>(Classe, Codigo)</c>.
/// </para>
/// </remarks>
public sealed class GrafoDependenciaConjunta
{
    private GrafoDependenciaConjunta(
        IReadOnlyList<NoGrafoDependencia> nos,
        IReadOnlyList<ArestaGrafoDependencia> arestas,
        IReadOnlyList<NoGrafoDependencia> ordemTopologica)
    {
        Nos = nos;
        Arestas = arestas;
        OrdemTopologica = ordemTopologica;
    }

    /// <summary>Todos os nós do grafo (campos, fatos e exigências), sem repetição.</summary>
    public IReadOnlyList<NoGrafoDependencia> Nos { get; }

    /// <summary>As arestas das quatro classes.</summary>
    public IReadOnlyList<ArestaGrafoDependencia> Arestas { get; }

    /// <summary>
    /// A ordenação topológica total e determinística (produtor antes do consumidor). O desempate
    /// entre nós sem relação de precedência é <c>(Classe, Codigo)</c> ordinal — total porque a
    /// identidade é única. A chave rica <c>(fase, ordem, idCanonico)</c> é da fatia de determinismo (§7).
    /// </summary>
    public IReadOnlyList<NoGrafoDependencia> OrdemTopologica { get; }

    /// <summary>
    /// Constrói e valida o grafo conjunto a partir das três dimensões da configuração que o
    /// alimentam: os fatos coletados (campo + fato declarado + pré-condição), as regras de derivação
    /// (fato derivado + dependências) e as exigências (gatilho). Devolve erro nomeado — nunca lança —
    /// quando as quatro classes de aresta juntas formam um ciclo.
    /// </summary>
    public static Result<GrafoDependenciaConjunta> Construir(
        IReadOnlyCollection<FatoColetado> fatosColetados,
        IReadOnlyCollection<ConfiguracaoDerivacaoFato> regrasDerivacao,
        IReadOnlyCollection<DocumentoExigido> documentosExigidos)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);
        ArgumentNullException.ThrowIfNull(regrasDerivacao);
        ArgumentNullException.ThrowIfNull(documentosExigidos);

        Dictionary<(ClasseNoGrafo, string), NoGrafoDependencia> nos = [];
        List<ArestaGrafoDependencia> arestas = [];

        NoGrafoDependencia No(ClasseNoGrafo classe, string codigo)
        {
            (ClasseNoGrafo, string) chave = (classe, codigo);
            if (!nos.TryGetValue(chave, out NoGrafoDependencia? no))
            {
                no = new NoGrafoDependencia(classe, codigo);
                nos[chave] = no;
            }

            return no;
        }

        // Um fato só tem nó de fato quando existe — declarado (produzido por campo) ou derivado (por
        // regra). Uma pré-condição/dependência/gatilho que cite fato inexistente não vira aresta
        // pendurada (a recusa por dependência não declarada é do congelamento, §7); aqui a aresta só
        // liga nós existentes, o que preserva a detecção de ciclo.
        HashSet<string> fatosExistentes = new(StringComparer.Ordinal);
        Dictionary<string, int> ordemDeclarada = new(StringComparer.Ordinal);
        foreach (FatoColetado fato in fatosColetados)
        {
            fatosExistentes.Add(fato.FatoCodigo);
            ordemDeclarada[fato.FatoCodigo] = fato.Ordem;
        }

        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            fatosExistentes.Add(config.CodigoFato);
        }

        // (1) Fatos declarados: nó de campo + nó de fato + aresta de produção campo → fato; e a
        // pré-condição vira aresta fato → campo (o campo é gatado pelos fatos que a pré-condição cita).
        foreach (FatoColetado fato in fatosColetados)
        {
            NoGrafoDependencia campo = No(ClasseNoGrafo.Campo, fato.FatoCodigo);
            NoGrafoDependencia noFato = No(ClasseNoGrafo.Fato, fato.FatoCodigo);
            arestas.Add(new ArestaGrafoDependencia(TipoArestaGrafo.Producao, campo, noFato));

            foreach (string citado in fato.FatosCitados)
            {
                if (fatosExistentes.Contains(citado))
                {
                    arestas.Add(new ArestaGrafoDependencia(
                        TipoArestaGrafo.Precondicao, No(ClasseNoGrafo.Fato, citado), campo));
                }
            }
        }

        // (2) Fatos derivados: nó de fato + aresta de derivação fato citado → fato derivado.
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            NoGrafoDependencia derivado = No(ClasseNoGrafo.Fato, config.CodigoFato);
            foreach (string citado in config.FatosCitados)
            {
                if (fatosExistentes.Contains(citado))
                {
                    arestas.Add(new ArestaGrafoDependencia(
                        TipoArestaGrafo.Derivacao, No(ClasseNoGrafo.Fato, citado), derivado));
                }
            }
        }

        // (3) Exigências: nó de exigência + aresta de gatilho fato → exigência. A exigência é
        // identificada pela sua identidade estável no processo; é sempre um sorvedouro (só arestas de
        // entrada), então nunca participa de um ciclo.
        foreach (DocumentoExigido documento in documentosExigidos)
        {
            NoGrafoDependencia exigencia = No(ClasseNoGrafo.Exigencia, documento.Id.ToString("N"));
            foreach (string citado in FatosDoGatilho(documento))
            {
                if (fatosExistentes.Contains(citado))
                {
                    arestas.Add(new ArestaGrafoDependencia(
                        TipoArestaGrafo.Gatilho, No(ClasseNoGrafo.Fato, citado), exigencia));
                }
            }
        }

        List<NoGrafoDependencia> todosOsNos = [.. nos.Values];
        Dictionary<NoGrafoDependencia, List<NoGrafoDependencia>> adjacencia =
            ConstruirAdjacencia(todosOsNos, arestas);

        // A detecção de ciclo usa desempate básico (Classe, Codigo) — a ordem de arranque só decide
        // QUAL ciclo é reportado, e a ordem de coleta rica exige o grafo já provado acíclico (a
        // recursão da ordem efetiva pressupõe um DAG).
        if (DetectarCiclo(todosOsNos, adjacencia, ComparadorBasico) is { } caminho)
        {
            return Result<GrafoDependenciaConjunta>.Failure(new DomainError(
                GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo,
                $"O grafo de dependência conjunto (produção, pré-condição, derivação e gatilho) forma "
                + $"um ciclo: {string.Join(" → ", caminho.Select(static n => n.Rotulo))}."));
        }

        // Ordem de coleta efetiva de cada nó: a MENOR posição configurada entre o próprio nó e todos
        // os que dele dependem (alcançáveis para a frente). Um fato derivado herda a posição do campo
        // que ele gata, sendo coletado logo antes dele, em vez de ir para o fim; e um campo posterior
        // não fura a fila de um anterior que ainda espera a derivação. Nós sem posição alguma no ramo
        // (exigências, sorvedouros) ficam na sentinela e ordenam por (Classe, Codigo). Recursão
        // memoizada, segura no DAG. O §7 promove isto à chave rica (fase, ordem, idCanonico).
        Dictionary<NoGrafoDependencia, int> ordemEfetiva = [];
        foreach (NoGrafoDependencia no in todosOsNos)
        {
            CalcularOrdemEfetiva(no, adjacencia, ordemDeclarada, ordemEfetiva);
        }

        Comparer<NoGrafoDependencia> comparadorNo = CriarComparadorNo(ordemEfetiva);
        Comparer<ArestaGrafoDependencia> comparadorAresta = CriarComparadorAresta(comparadorNo);

        List<NoGrafoDependencia> ordem = OrdenarTopologicamente(todosOsNos, adjacencia, comparadorNo);

        // Nós e arestas expostos em ordem canônica, para que a mesma configuração produza o mesmo
        // grafo observável independentemente da ordem de enumeração das componentes de entrada.
        todosOsNos.Sort(comparadorNo);
        arestas.Sort(comparadorAresta);
        return Result<GrafoDependenciaConjunta>.Success(
            new GrafoDependenciaConjunta(todosOsNos, arestas, ordem));
    }

    private static IReadOnlyCollection<string> FatosDoGatilho(DocumentoExigido documento) =>
        [.. documento.Condicoes.Select(static c => c.Fato).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// A posição de coleta efetiva de um nó: o mínimo entre a sua própria posição configurada (só
    /// campo/fato declarado a tem) e a posição efetiva de todos os nós que dele dependem. Memoizada;
    /// pressupõe o grafo acíclico. Faz um nó intermédio (fato derivado, campo-gate) ser ordenado pela
    /// posição do campo mais cedo que ele desbloqueia.
    /// </summary>
    private static int CalcularOrdemEfetiva(
        NoGrafoDependencia no,
        Dictionary<NoGrafoDependencia, List<NoGrafoDependencia>> adjacencia,
        Dictionary<string, int> ordemDeclarada,
        Dictionary<NoGrafoDependencia, int> memo)
    {
        if (memo.TryGetValue(no, out int cached))
        {
            return cached;
        }

        int melhor = no.Classe is ClasseNoGrafo.Campo or ClasseNoGrafo.Fato
            && ordemDeclarada.TryGetValue(no.Codigo, out int propria)
                ? propria
                : int.MaxValue;

        foreach (NoGrafoDependencia sucessor in adjacencia[no])
        {
            melhor = Math.Min(melhor, CalcularOrdemEfetiva(sucessor, adjacencia, ordemDeclarada, memo));
        }

        memo[no] = melhor;
        return melhor;
    }

    private static Dictionary<NoGrafoDependencia, List<NoGrafoDependencia>> ConstruirAdjacencia(
        IReadOnlyList<NoGrafoDependencia> nos, IReadOnlyList<ArestaGrafoDependencia> arestas)
    {
        Dictionary<NoGrafoDependencia, List<NoGrafoDependencia>> adjacencia = [];
        foreach (NoGrafoDependencia no in nos)
        {
            adjacencia[no] = [];
        }

        foreach (ArestaGrafoDependencia aresta in arestas)
        {
            adjacencia[aresta.Origem].Add(aresta.Destino);
        }

        return adjacencia;
    }

    /// <summary>
    /// Busca em profundidade com marcação tricolor sobre o grafo conjunto, devolvendo o caminho do
    /// primeiro ciclo encontrado — ou <see langword="null"/> quando é acíclico. Mesma técnica da
    /// detecção do sub-grafo de pré-condições (§2), generalizada às quatro classes de aresta.
    /// </summary>
    private static IReadOnlyList<NoGrafoDependencia>? DetectarCiclo(
        IReadOnlyList<NoGrafoDependencia> nos,
        IReadOnlyDictionary<NoGrafoDependencia, List<NoGrafoDependencia>> adjacencia,
        IComparer<NoGrafoDependencia> comparador)
    {
        HashSet<NoGrafoDependencia> emVisita = [];
        HashSet<NoGrafoDependencia> concluidos = [];
        List<NoGrafoDependencia> pilha = [];

        // Ordem estável de arranque para tornar o ciclo reportado determinístico.
        foreach (NoGrafoDependencia no in nos.OrderBy(n => n, comparador))
        {
            if (Visitar(no, adjacencia, comparador, emVisita, concluidos, pilha) is { } caminho)
            {
                return caminho;
            }
        }

        return null;
    }

    private static IReadOnlyList<NoGrafoDependencia>? Visitar(
        NoGrafoDependencia no,
        IReadOnlyDictionary<NoGrafoDependencia, List<NoGrafoDependencia>> adjacencia,
        IComparer<NoGrafoDependencia> comparador,
        HashSet<NoGrafoDependencia> emVisita,
        HashSet<NoGrafoDependencia> concluidos,
        List<NoGrafoDependencia> pilha)
    {
        if (concluidos.Contains(no))
        {
            return null;
        }

        if (!emVisita.Add(no))
        {
            // Reencontro de um nó ainda na pilha: o ciclo é o trecho da pilha a partir dele.
            int inicio = pilha.IndexOf(no);
            return [.. pilha[inicio..], no];
        }

        pilha.Add(no);
        foreach (NoGrafoDependencia vizinho in adjacencia[no].OrderBy(n => n, comparador))
        {
            if (Visitar(vizinho, adjacencia, comparador, emVisita, concluidos, pilha) is { } caminho)
            {
                return caminho;
            }
        }

        pilha.RemoveAt(pilha.Count - 1);
        emVisita.Remove(no);
        concluidos.Add(no);
        return null;
    }

    /// <summary>
    /// Ordenação topológica total e determinística (Kahn): entre os nós prontos (grau de entrada
    /// zero), emite sempre o menor pelo <paramref name="comparador"/> — ordem de coleta configurada,
    /// depois <c>(Classe, Codigo)</c>. Só é chamada num grafo já provado acíclico, então emite todos.
    /// </summary>
    private static List<NoGrafoDependencia> OrdenarTopologicamente(
        IReadOnlyList<NoGrafoDependencia> nos,
        IReadOnlyDictionary<NoGrafoDependencia, List<NoGrafoDependencia>> adjacencia,
        IComparer<NoGrafoDependencia> comparador)
    {
        Dictionary<NoGrafoDependencia, int> grauEntrada = [];
        foreach (NoGrafoDependencia no in nos)
        {
            grauEntrada[no] = 0;
        }

        foreach (List<NoGrafoDependencia> destinos in adjacencia.Values)
        {
            foreach (NoGrafoDependencia destino in destinos)
            {
                grauEntrada[destino]++;
            }
        }

        List<NoGrafoDependencia> prontos = [.. nos.Where(n => grauEntrada[n] == 0)];
        List<NoGrafoDependencia> ordem = [];
        while (prontos.Count > 0)
        {
            prontos.Sort(comparador);
            NoGrafoDependencia atual = prontos[0];
            prontos.RemoveAt(0);
            ordem.Add(atual);

            foreach (NoGrafoDependencia vizinho in adjacencia[atual])
            {
                if (--grauEntrada[vizinho] == 0)
                {
                    prontos.Add(vizinho);
                }
            }
        }

        return ordem;
    }

    /// <summary>Desempate sem posição de coleta — só para a ordem de arranque da detecção de ciclo.</summary>
    private static readonly Comparer<NoGrafoDependencia> ComparadorBasico =
        Comparer<NoGrafoDependencia>.Create(static (a, b) =>
        {
            int porClasse = ((int)a.Classe).CompareTo((int)b.Classe);
            return porClasse != 0 ? porClasse : string.CompareOrdinal(a.Codigo, b.Codigo);
        });

    private static Comparer<NoGrafoDependencia> CriarComparadorNo(
        Dictionary<NoGrafoDependencia, int> ordemEfetiva) =>
        Comparer<NoGrafoDependencia>.Create((a, b) =>
        {
            int porOrdem = ordemEfetiva[a].CompareTo(ordemEfetiva[b]);
            if (porOrdem != 0)
            {
                return porOrdem;
            }

            int porClasse = ((int)a.Classe).CompareTo((int)b.Classe);
            return porClasse != 0 ? porClasse : string.CompareOrdinal(a.Codigo, b.Codigo);
        });

    private static Comparer<ArestaGrafoDependencia> CriarComparadorAresta(
        Comparer<NoGrafoDependencia> comparadorNo) =>
        Comparer<ArestaGrafoDependencia>.Create((a, b) =>
        {
            int porTipo = ((int)a.Tipo).CompareTo((int)b.Tipo);
            if (porTipo != 0)
            {
                return porTipo;
            }

            int porOrigem = comparadorNo.Compare(a.Origem, b.Origem);
            return porOrigem != 0 ? porOrigem : comparadorNo.Compare(a.Destino, b.Destino);
        });
}

/// <summary>Códigos de erro de <see cref="GrafoDependenciaConjunta"/>.</summary>
public static class GrafoDependenciaConjuntaErrorCodes
{
    public const string GrafoConjuntoComCiclo = "GrafoDependenciaConjunta.GrafoConjuntoComCiclo";
}
