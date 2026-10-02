namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O grafo de dependência conjunto de um processo (Story #928, §6): torna <b>explícito</b> o que a
/// configuração hoje expressa implicitamente (ordem de coleta, pré-condições, dependências de
/// derivação e gatilhos). Os nós são campos, fatos, exigências, seções com exibição condicional e
/// termos com condição; as quatro classes de aresta são
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

    /// <summary>Todos os nós do grafo (campos, fatos, exigências, seções com exibição condicional e termos com condição), sem repetição.</summary>
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
    /// Constrói e valida o grafo conjunto a partir das dimensões da configuração que o alimentam: os
    /// fatos coletados (campo + fato declarado + as regras do item), as regras de derivação (fato
    /// derivado + dependências), as exigências (gatilho), as seções com exibição (que gatam os seus
    /// campos) e os termos com condição. A posição de coleta é a finalidade do formulário e depois a
    /// ordem dentro dele (UNI-REQ-0078). Os campos dos grupos repetíveis (UNI-REQ-0146) são campos
    /// como os itens, na posição do grupo, gatados pelas regras do grupo. Devolve erro nomeado —
    /// nunca lança — quando as quatro classes de aresta juntas formam um ciclo.
    /// </summary>
    public static Result<GrafoDependenciaConjunta> Construir(
        IReadOnlyCollection<FatoColetado> fatosColetados,
        IReadOnlyCollection<ConfiguracaoDerivacaoFato> regrasDerivacao,
        IReadOnlyCollection<DocumentoExigido> documentosExigidos,
        IReadOnlyCollection<FormularioProcesso> formularios,
        IReadOnlyCollection<TermoExigidoFormulario> termos,
        IReadOnlyCollection<GrupoColetado>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);
        grupos ??= [];
        ArgumentNullException.ThrowIfNull(regrasDerivacao);
        ArgumentNullException.ThrowIfNull(documentosExigidos);
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(termos);

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
        Dictionary<NoGrafoDependencia, long> posicaoDeclarada = [];
        foreach (FatoColetado fato in fatosColetados)
        {
            fatosExistentes.Add(fato.FatoCodigo);
            posicaoDeclarada[No(ClasseNoGrafo.Campo, fato.FatoCodigo)] = Posicao(fato.Finalidade, fato.Ordem);
            posicaoDeclarada[No(ClasseNoGrafo.Fato, fato.FatoCodigo)] = Posicao(fato.Finalidade, fato.Ordem);
        }

        foreach (GrupoColetado grupo in grupos)
        {
            foreach (FatoColetado subitem in grupo.Subitens)
            {
                fatosExistentes.Add(subitem.FatoCodigo);
                posicaoDeclarada[No(ClasseNoGrafo.Campo, subitem.FatoCodigo)] = Posicao(grupo.Finalidade, grupo.Ordem);
                posicaoDeclarada[No(ClasseNoGrafo.Fato, subitem.FatoCodigo)] = Posicao(grupo.Finalidade, grupo.Ordem);
            }
        }

        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            fatosExistentes.Add(config.CodigoFato);
        }

        // O derivado do sistema citado por alguma regra existe quando o processo tem todas as
        // dependências declaradas pelo mecanismo dele; o que nenhuma regra cita fica fora do grafo.
        HashSet<string> citados = new(
            fatosColetados.SelectMany(static f => f.FatosCitados)
                .Concat(grupos.SelectMany(static g => g.FatosCitados.Concat(g.Subitens.SelectMany(static s => s.FatosCitados))))
                .Concat(regrasDerivacao.SelectMany(static r => r.FatosCitados))
                .Concat(documentosExigidos.SelectMany(FatosDoGatilho))
                .Concat(formularios.SelectMany(static f => f.Etapas).SelectMany(static e => e.FatosCitados))
                .Concat(termos.SelectMany(static t => t.FatosCitados)),
            StringComparer.Ordinal);
        KeyValuePair<string, IReadOnlyList<string>>[] derivadosDoSistema = [.. DerivadosDoSistema.Dependencias
            .Where(d => citados.Contains(d.Key) && d.Value.All(fatosExistentes.Contains))
            .OrderBy(static d => d.Key, StringComparer.Ordinal)];
        foreach ((string derivado, _) in derivadosDoSistema)
        {
            fatosExistentes.Add(derivado);
        }

        // (1) Fatos declarados: nó de campo + nó de fato + aresta de produção campo → fato; e a
        // pré-condição vira aresta fato → campo (o campo é gatado pelos fatos que a pré-condição cita).
        // O campo de grupo é gatado também pelas regras do grupo dele.
        Dictionary<string, IReadOnlyCollection<string>> regrasDoGrupoDoCampo = grupos
            .SelectMany(static g => g.Subitens.Select(s => (s.FatoCodigo, g.FatosCitados)))
            .ToDictionary(static p => p.FatoCodigo, static p => p.FatosCitados, StringComparer.Ordinal);
        foreach (FatoColetado fato in fatosColetados.Concat(grupos.SelectMany(static g => g.Subitens)))
        {
            NoGrafoDependencia campo = No(ClasseNoGrafo.Campo, fato.FatoCodigo);
            NoGrafoDependencia noFato = No(ClasseNoGrafo.Fato, fato.FatoCodigo);
            arestas.Add(new ArestaGrafoDependencia(TipoArestaGrafo.Producao, campo, noFato));

            IEnumerable<string> doGrupo = regrasDoGrupoDoCampo.TryGetValue(fato.FatoCodigo, out IReadOnlyCollection<string>? doSeuGrupo) ? doSeuGrupo : [];
            foreach (string citado in fato.FatosCitados.Concat(doGrupo).Distinct(StringComparer.Ordinal))
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

        // (2b) Derivados do sistema: aresta de derivação dependência → derivado.
        foreach ((string derivado, IReadOnlyList<string> dependencias) in derivadosDoSistema)
        {
            NoGrafoDependencia noDerivado = No(ClasseNoGrafo.Fato, derivado);
            foreach (string dependencia in dependencias)
            {
                arestas.Add(new ArestaGrafoDependencia(TipoArestaGrafo.Derivacao, No(ClasseNoGrafo.Fato, dependencia), noDerivado));
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

        // (4) Seções com exibição condicional: nó de seção + pré-condição fato citado → seção, e a
        // seção gata cada campo dela (seção → campo). Seção sem exibição não acrescenta dependência.
        foreach (FormularioProcesso formulario in formularios)
        {
            foreach (EtapaFormulario secao in formulario.Etapas.Where(static e => e.Exibicao is not null))
            {
                NoGrafoDependencia noSecao = No(ClasseNoGrafo.Secao, CodigoNoFormulario(formulario.Finalidade, secao.Codigo));
                posicaoDeclarada[noSecao] = PosicaoDaSecao(formulario, secao, fatosColetados, grupos);
                foreach (string citado in secao.FatosCitados.Where(fatosExistentes.Contains))
                {
                    arestas.Add(new ArestaGrafoDependencia(TipoArestaGrafo.Precondicao, No(ClasseNoGrafo.Fato, citado), noSecao));
                }

                IEnumerable<FatoColetado> camposDaSecao = fatosColetados
                    .Where(f => f.Finalidade == formulario.Finalidade && string.Equals(f.EtapaCodigo, secao.Codigo, StringComparison.Ordinal))
                    .Concat(grupos
                        .Where(g => g.Finalidade == formulario.Finalidade && string.Equals(g.EtapaCodigo, secao.Codigo, StringComparison.Ordinal))
                        .SelectMany(static g => g.Subitens));
                foreach (FatoColetado campo in camposDaSecao)
                {
                    arestas.Add(new ArestaGrafoDependencia(TipoArestaGrafo.Precondicao, noSecao, No(ClasseNoGrafo.Campo, campo.FatoCodigo)));
                }
            }
        }

        // (5) Termos com condição: nó de termo, depois de todos os campos do formulário dele e na
        // ordem dos termos, + pré-condição fato citado → termo.
        foreach (TermoExigidoFormulario termo in termos.Where(static t => t.FatosCitados.Count > 0))
        {
            NoGrafoDependencia noTermo = No(ClasseNoGrafo.Termo, CodigoNoFormulario(termo.Finalidade, termo.Codigo));
            posicaoDeclarada[noTermo] = Posicao(termo.Finalidade, termo.Ordem, depoisDosCampos: true);
            foreach (string citado in termo.FatosCitados.Where(fatosExistentes.Contains))
            {
                arestas.Add(new ArestaGrafoDependencia(TipoArestaGrafo.Precondicao, No(ClasseNoGrafo.Fato, citado), noTermo));
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
        Dictionary<NoGrafoDependencia, long> ordemEfetiva = [];
        foreach (NoGrafoDependencia no in todosOsNos)
        {
            CalcularOrdemEfetiva(no, adjacencia, posicaoDeclarada, ordemEfetiva);
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

    /// <summary>
    /// A posição de coleta de um nó: primeiro a finalidade do formulário, na ordem em que o
    /// candidato os preenche; depois os campos, e só então os termos; e a ordem dentro de cada um —
    /// a ordem é única só dentro de cada formulário. A seção fica imediatamente antes do campo que
    /// tem a mesma ordem, porque ela o precede no formulário.
    /// </summary>
    private static long Posicao(FinalidadeFormulario finalidade, int ordem, bool depoisDosCampos = false, bool antesDoCampo = false) =>
        ((long)finalidade << 35) | ((depoisDosCampos ? 1L : 0L) << 34) | (((long)(uint)ordem << 1) | (antesDoCampo ? 0L : 1L));

    /// <summary>
    /// A posição da seção: a do primeiro item ou grupo dela ou, se está vazia, das seções seguintes;
    /// sem nenhum em nenhuma delas, depois de todos os campos do formulário.
    /// </summary>
    private static long PosicaoDaSecao(
        FormularioProcesso formulario, EtapaFormulario secao, IEnumerable<FatoColetado> fatosColetados, IEnumerable<GrupoColetado> grupos)
    {
        HashSet<string> daquiEmDiante = new(
            formulario.Etapas.Where(e => e.Ordem >= secao.Ordem).Select(static e => e.Codigo), StringComparer.Ordinal);
        int ordem = fatosColetados.Select(static f => (f.Finalidade, f.EtapaCodigo, f.Ordem))
            .Concat(grupos.Select(static g => (g.Finalidade, g.EtapaCodigo, g.Ordem)))
            .Where(p => p.Finalidade == formulario.Finalidade && p.EtapaCodigo is { } etapa && daquiEmDiante.Contains(etapa))
            .Select(static p => p.Ordem)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        return Posicao(formulario.Finalidade, ordem, antesDoCampo: true);
    }

    /// <summary>O código do nó de seção ou de termo: a finalidade e o código, que só é único no formulário.</summary>
    private static string CodigoNoFormulario(FinalidadeFormulario finalidade, string codigo) =>
        $"{EstruturaFormulario.ParaToken(finalidade)}.{codigo}";

    private static IReadOnlyCollection<string> FatosDoGatilho(DocumentoExigido documento) =>
        [.. documento.Condicoes.Select(static c => c.Fato).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// A posição de coleta efetiva de um nó: o mínimo entre a sua própria posição configurada (a
    /// têm o campo, o fato declarado e o termo) e a posição efetiva de todos os nós que dele
    /// dependem. Memoizada; pressupõe o grafo acíclico. Faz um nó intermédio (fato derivado, seção,
    /// campo-gate) ser ordenado pela posição do campo mais cedo que ele desbloqueia.
    /// </summary>
    private static long CalcularOrdemEfetiva(
        NoGrafoDependencia no,
        Dictionary<NoGrafoDependencia, List<NoGrafoDependencia>> adjacencia,
        Dictionary<NoGrafoDependencia, long> posicaoDeclarada,
        Dictionary<NoGrafoDependencia, long> memo)
    {
        if (memo.TryGetValue(no, out long cached))
        {
            return cached;
        }

        long melhor = posicaoDeclarada.TryGetValue(no, out long propria) ? propria : long.MaxValue;

        foreach (NoGrafoDependencia sucessor in adjacencia[no])
        {
            melhor = Math.Min(melhor, CalcularOrdemEfetiva(sucessor, adjacencia, posicaoDeclarada, memo));
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
        Dictionary<NoGrafoDependencia, long> ordemEfetiva) =>
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
