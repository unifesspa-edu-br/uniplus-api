namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Linq;
using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// Resolve, para um candidato, o veredicto de cada nó de uma <see cref="ArvoreExigenciasCongelada"/>
/// — estado de satisfação de 5 valores (<see cref="EstadoSatisfacao"/>, compondo com o gatilho
/// ternário da folha) e as consequências que VIGORAM pela fronteira ativa (Story #920). Substitui
/// <c>ResolvedorExigenciasDocumentais</c> (grupo plano) — mesmos princípios: puro, erros nomeados
/// nunca resultado vazio (ADR-0076), nunca chamado dentro da transação de publicação.
/// </summary>
/// <remarks>
/// Álgebra (spec <c>documentos-exigidos-composicao</c>, requirements "Álgebra ternária de
/// satisfação" e "Consequência por nó e fronteira ativa de emissão"):
/// <list type="bullet">
/// <item><b>E</b>: filhos <see cref="EstadoSatisfacao.NaoAplicavel"/> são ignorados; todos
/// não-aplicáveis ⇒ não-aplicável; senão <see cref="EstadoSatisfacao.Impossivel"/> se algum
/// restante impossível; satisfeito se todos restantes satisfeitos; pendente se algum restante
/// pendente; senão indeterminado.</item>
/// <item><b>OU/N-de</b> (mínimo N): todos não-aplicáveis ⇒ não-aplicável; satisfeito se
/// ≥N satisfeitos; senão, M = satisfeitos+pendentes+indeterminados (máximo atingível) — impossível
/// se M&lt;N; indeterminado se algum indeterminado; senão pendente.</item>
/// <item><b>Fronteira ativa</b> (raiz→baixo): satisfeito/não-aplicável suprime a subárvore; folha
/// pendente/indeterminada/impossível emite a própria consequência; OU/N-de opaco pendente emite a
/// SUA própria (filhos viram só pendência de orientação, não vigente); E transparente não emite e
/// desce para os filhos não satisfeitos e não-não-aplicáveis (nunca pai e filho juntos).</item>
/// <item><b>Repetição por entidade</b> (Story #922, ADR-0138): uma subárvore <c>repetePorEntidade</c>
/// é avaliada UMA VEZ POR OCORRÊNCIA do grupo repetível que ela nomeia — fatos do candidato
/// mesclados com os campos da ocorrência (sujeito trocado, mesmo motor de gatilho), apresentações
/// filtradas por <c>entidade_id</c>. O agregado sobe como um <c>E</c> entre instâncias (todas
/// precisam satisfazer); sem instância declarada, a subárvore é não-aplicável. A fronteira emite
/// UMA consequência por instância pendente, tagueada com <c>entidade_id</c> — nunca um valor
/// global único (perderia QUAL instância está pendente). Repetição não aninha (validado no
/// cadastro, <see cref="NoExigencia.CriarGrupo"/>).</item>
/// </list>
/// </remarks>
public static class ResolvedorArvoreSatisfacao
{
    public static Result<ResultadoResolucaoArvore> Resolver(
        ArvoreExigenciasCongelada arvore,
        IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoesPorExigenciaId,
        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>>? instanciasPorGrupo = null,
        IReadOnlySet<Guid>? exigenciasBloqueadas = null)
    {
        ArgumentNullException.ThrowIfNull(arvore);
        ArgumentNullException.ThrowIfNull(fatosResolvidos);
        ArgumentNullException.ThrowIfNull(apresentacoesPorExigenciaId);

        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasOuNenhuma(instanciasPorGrupo);
        if (ConferenciaDaEntradaDaArvore.Pendencia(arvore, instancias) is { } pendencia)
        {
            return Result<ResultadoResolucaoArvore>.Failure(pendencia);
        }

        return Result<ResultadoResolucaoArvore>.Success(ResolverRaizes(
            arvore, fatosResolvidos, apresentacoesPorExigenciaId, instancias, BloqueadasOuNenhuma(exigenciasBloqueadas)));
    }

    private static ResultadoResolucaoArvore ResolverRaizes(
        ArvoreExigenciasCongelada arvore,
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> instanciasPorGrupo,
        IReadOnlySet<Guid> bloqueadas)
    {
        PercursoDaArvore percurso = new(bloqueadas);
        foreach (NoExigencia raiz in arvore.Raizes)
        {
            EstadoSatisfacao estadoRaiz = ResolverNo(
                raiz, fatos, apresentacoes, instanciasPorGrupo, bloqueadas,
                percurso.Estados, percurso.StatusPorExigencia, percurso.InstanciasResolvidasPorNo);
            percurso.Fechar(raiz, estadoRaiz);
        }

        return percurso.Resultado();
    }

    // A fronteira de disponibilidade por candidato: uma exigência cujo fato gatilho ainda não alcançou a
    // fronteira (produtor não executado ou gate de fase não satisfeito) está BLOQUEADA. BLOQUEADO não é um
    // 6º estado da árvore — é máscara+projeção: a folha projeta-se em INDETERMINADO na agregação (mantém o
    // pai pendente) E tem a emissão suprimida pelo predicado recursivo emissionBlocked. Ausência = nada
    // bloqueado (a resolução no ponto devido, sem fronteira a mascarar).
    private static IReadOnlySet<Guid> BloqueadasOuNenhuma(IReadOnlySet<Guid>? exigenciasBloqueadas) =>
        exigenciasBloqueadas ?? new HashSet<Guid>();

    private static IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> InstanciasOuNenhuma(
        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>>? instanciasPorGrupo) =>
        instanciasPorGrupo ?? new Dictionary<string, IReadOnlyList<InstanciaEntidade>>(StringComparer.Ordinal);

    private static EstadoSatisfacao ResolverNo(
        NoExigencia no,
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> instanciasPorGrupo,
        IReadOnlySet<Guid> bloqueadas,
        Dictionary<Guid, EstadoSatisfacao> estados,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia,
        Dictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        EstadoSatisfacao estado = no.RepetePorEntidade is { } grupo
            ? ResolverNoRepetido(no, grupo, fatos, apresentacoes, instanciasPorGrupo, bloqueadas, instanciasResolvidasPorNo)
            : no.Tipo == TipoNo.Folha
                ? ResolucaoDaFolha.Resolver(no, fatos, apresentacoes, bloqueadas, statusPorExigencia)
                : ResolverGrupo(no, fatos, apresentacoes, instanciasPorGrupo, bloqueadas, estados, statusPorExigencia, instanciasResolvidasPorNo);

        estados[no.Id] = estado;
        return estado;
    }

    /// <summary>
    /// Story #922 — resolve uma subárvore <c>repetePorEntidade</c> UMA VEZ POR INSTÂNCIA
    /// declarada, com dicionários LOCAIS por instância (nunca os globais — o estado de um
    /// descendente dentro da repetição é inerentemente por-instância, não um valor único).
    /// </summary>
    private static EstadoSatisfacao ResolverNoRepetido(
        NoExigencia no,
        string grupo,
        IReadOnlyDictionary<string, FatoResolvido> fatosCandidato,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> instanciasPorGrupo,
        IReadOnlySet<Guid> bloqueadas,
        Dictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        IReadOnlyList<InstanciaEntidade> instancias = instanciasPorGrupo.GetValueOrDefault(grupo, []);
        if (instancias.Count == 0)
        {
            // Nenhuma instância declarada: nada a multiplicar — mesmo raciocínio de um grupo
            // cujos filhos são todos não-aplicáveis.
            instanciasResolvidasPorNo[no.Id] = [];
            return EstadoSatisfacao.NaoAplicavel;
        }

        List<InstanciaResolvida> resolvidas = [];
        foreach (InstanciaEntidade instancia in instancias)
        {
            Dictionary<string, FatoResolvido> fatosDaInstancia = MesclarFatosDeEntidade(fatosCandidato, instancia.Atributos);
            IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoesDaInstancia =
                FiltrarApresentacoesPorEntidade(apresentacoes, instancia.EntidadeId);

            Dictionary<Guid, EstadoSatisfacao> estadosDaInstancia = [];
            Dictionary<Guid, StatusResolucaoExigencia> statusDaInstancia = [];
            // Aninhamento já é recusado no cadastro (NoExigencia.CriarGrupo) — nenhum
            // descendente aqui dentro pode ser, ele mesmo, repetePorEntidade; um dicionário
            // vazio é seguro (nunca populado nesta subárvore).
            Dictionary<Guid, List<InstanciaResolvida>> semRepeticaoAninhada = [];
            // A fronteira de disponibilidade vale também dentro da repetição: a folha bloqueada de uma
            // ocorrência projeta indeterminado e não emite, como fora dela.
            EstadoSatisfacao estadoInstancia = no.Tipo == TipoNo.Folha
                ? ResolucaoDaFolha.Resolver(no, fatosDaInstancia, apresentacoesDaInstancia, bloqueadas, statusDaInstancia)
                : ResolverGrupo(
                    no, fatosDaInstancia, apresentacoesDaInstancia, instanciasPorGrupo, bloqueadas,
                    estadosDaInstancia, statusDaInstancia, semRepeticaoAninhada);
            estadosDaInstancia[no.Id] = estadoInstancia;

            HashSet<Guid> suprimidosDaInstancia = [];
            FronteiraDeEmissao.CalcularEmissionBlocked(no, estadosDaInstancia, bloqueadas, suprimidosDaInstancia, naInstancia: true);
            resolvidas.Add(new InstanciaResolvida(
                instancia.EntidadeId, estadoInstancia, estadosDaInstancia, statusDaInstancia, suprimidosDaInstancia));
        }

        instanciasResolvidasPorNo[no.Id] = resolvidas;

        // Agregação entre instâncias: a subárvore só está satisfeita quando TODAS as instâncias
        // declaradas satisfazem — mesma álgebra do E (o ramo "todas não-aplicáveis" não ocorre
        // aqui na prática, já tratado como caso "zero instâncias" acima; ResolverE cobre por
        // defesa em profundidade mesmo assim).
        return ResolverE([.. resolvidas.Select(static r => r.Estado)]);
    }

    private static Dictionary<string, FatoResolvido> MesclarFatosDeEntidade(
        IReadOnlyDictionary<string, FatoResolvido> fatosCandidato, IReadOnlyDictionary<string, FatoResolvido> atributosDaInstancia)
    {
        Dictionary<string, FatoResolvido> mesclado = new(fatosCandidato, StringComparer.Ordinal);
        foreach (KeyValuePair<string, FatoResolvido> atributo in atributosDaInstancia)
        {
            mesclado[atributo.Key] = atributo.Value;
        }

        return mesclado;
    }

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> FiltrarApresentacoesPorEntidade(
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes, string entidadeId)
    {
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> filtradas = [];
        foreach (KeyValuePair<Guid, IReadOnlyList<ApresentacaoDocumento>> par in apresentacoes)
        {
            List<ApresentacaoDocumento> daEntidade = [.. par.Value.Where(a => a.EntidadeId == entidadeId)];
            if (daEntidade.Count > 0)
            {
                filtradas[par.Key] = daEntidade;
            }
        }

        return filtradas;
    }

    private static EstadoSatisfacao ResolverGrupo(
        NoExigencia no,
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> instanciasPorGrupo,
        IReadOnlySet<Guid> bloqueadas,
        Dictionary<Guid, EstadoSatisfacao> estados,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia,
        Dictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        List<EstadoSatisfacao> estadosFilhos = [.. no.Filhos.Select(filho =>
            ResolverNo(filho, fatos, apresentacoes, instanciasPorGrupo, bloqueadas, estados, statusPorExigencia, instanciasResolvidasPorNo))];

        return no.Tipo == TipoNo.GrupoE
            ? ResolverE(estadosFilhos)
            : ResolverOu(estadosFilhos, no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao);
    }

    private static EstadoSatisfacao ResolverE(IReadOnlyList<EstadoSatisfacao> filhos)
    {
        List<EstadoSatisfacao> restantes = [.. filhos.Where(static e => e != EstadoSatisfacao.NaoAplicavel)];
        if (restantes.Count == 0)
        {
            return EstadoSatisfacao.NaoAplicavel;
        }

        if (restantes.Any(static e => e == EstadoSatisfacao.Impossivel))
        {
            return EstadoSatisfacao.Impossivel;
        }

        if (restantes.All(static e => e == EstadoSatisfacao.Satisfeito))
        {
            return EstadoSatisfacao.Satisfeito;
        }

        if (restantes.Any(static e => e == EstadoSatisfacao.Pendente))
        {
            return EstadoSatisfacao.Pendente;
        }

        return EstadoSatisfacao.Indeterminado;
    }

    private static EstadoSatisfacao ResolverOu(IReadOnlyList<EstadoSatisfacao> filhos, int quantidadeMinima)
    {
        if (filhos.All(static e => e == EstadoSatisfacao.NaoAplicavel))
        {
            return EstadoSatisfacao.NaoAplicavel;
        }

        int satisfeitos = filhos.Count(static e => e == EstadoSatisfacao.Satisfeito);
        if (satisfeitos >= quantidadeMinima)
        {
            return EstadoSatisfacao.Satisfeito;
        }

        int pendentes = filhos.Count(static e => e == EstadoSatisfacao.Pendente);
        int indeterminados = filhos.Count(static e => e == EstadoSatisfacao.Indeterminado);
        int maximoAtingivel = satisfeitos + pendentes + indeterminados;

        if (maximoAtingivel < quantidadeMinima)
        {
            return EstadoSatisfacao.Impossivel;
        }

        return indeterminados > 0 ? EstadoSatisfacao.Indeterminado : EstadoSatisfacao.Pendente;
    }
}
