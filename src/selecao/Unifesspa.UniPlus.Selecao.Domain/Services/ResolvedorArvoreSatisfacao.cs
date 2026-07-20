namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Linq;
using System.Text.Json;

using Entities;
using Enums;
using Kernel.Results;
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
/// <item><b>Repetição por entidade</b> (Story #922): uma subárvore <c>repetePorEntidade</c> é
/// avaliada UMA VEZ POR INSTÂNCIA que o candidato declarar desse tipo — fatos do candidato
/// mesclados com os atributos da instância (sujeito trocado, mesmo motor de gatilho), apresentações
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
        ArvoreExigenciasCongelada? arvore,
        IReadOnlyDictionary<string, JsonElement> fatosResolvidos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoesPorExigenciaId,
        IReadOnlyDictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>>? instanciasPorTipoEntidade = null)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);
        ArgumentNullException.ThrowIfNull(apresentacoesPorExigenciaId);

        if (arvore is null)
        {
            return Result<ResultadoResolucaoArvore>.Failure(new DomainError(
                "ResolvedorArvoreSatisfacao.ArvoreAusente",
                "Não há versão vigente congelada para resolver — nenhuma árvore de exigências documentais para avaliar."));
        }

        List<NoExigencia> todosOsNos = [.. arvore.Raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];
        List<NoExigencia> folhas = [.. todosOsNos.Where(static no => no.Tipo == TipoNo.Folha)];

        // Defesa em profundidade (ADR-0076: erros nomeados, nunca exceção) — folha sem
        // DocumentoExigido só alcançaria este método via Reidratar mal-formado (nunca via
        // NoExigencia.CriarFolha, que exige o documento); não confia na invariante de outra
        // camada antes de desreferenciar.
        if (folhas.Any(static no => no.DocumentoExigidoId is null || no.DocumentoExigido is null))
        {
            return Result<ResultadoResolucaoArvore>.Failure(new DomainError(
                "ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida",
                "A árvore congelada tem folha sem DocumentoExigido — toda folha precisa envolver uma exigência."));
        }

        List<Guid> idsDeFolhas = [.. folhas.Select(static no => no.DocumentoExigidoId!.Value)];
        if (idsDeFolhas.Distinct().Count() != idsDeFolhas.Count)
        {
            return Result<ResultadoResolucaoArvore>.Failure(new DomainError(
                "ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida",
                "A árvore congelada tem exigenciaId repetido entre folhas — cada exigência precisa de identidade única (CA-09)."));
        }

        // Story #922 — defesa em profundidade (mesmo raciocínio das duas checagens acima):
        // NoExigencia.CriarGrupo recusa catálogo forjado e aninhamento na ESCRITA, mas
        // Reidratar confia no dado congelado sem revalidar — um envelope corrompido não pode
        // silenciosamente suprimir consequências de uma repetição aninhada não detectada.
        if (todosOsNos.Any(static no => no.RepetePorEntidade is { } tipo && !Enum.IsDefined(tipo)))
        {
            return Result<ResultadoResolucaoArvore>.Failure(new DomainError(
                "ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida",
                "A árvore congelada tem repetePorEntidade fora do catálogo fechado — cada nó marcado precisa de um TipoEntidade válido."));
        }

        if (arvore.Raizes.Any(ContemRepeticaoAninhada))
        {
            return Result<ResultadoResolucaoArvore>.Failure(new DomainError(
                "ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida",
                "A árvore congelada tem repetição por entidade aninhada — uma subárvore repetePorEntidade não pode conter outra."));
        }

        IReadOnlyDictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instancias =
            instanciasPorTipoEntidade ?? new Dictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>>();

        // Story #922 — defesa em profundidade sobre o insumo do CHAMADOR (não da árvore
        // congelada): duas instâncias do mesmo tipo com o mesmo entidade_id (ou um
        // entidade_id vazio/em branco) tornariam a correlação (exigencia_id, tipoEntidade,
        // entidade_id) ambígua — apresentações e estados de uma instância vazariam para outra.
        foreach ((TipoEntidade tipoEntidade, IReadOnlyList<InstanciaEntidade> instanciasDoTipo) in instancias)
        {
            List<string> ids = [.. instanciasDoTipo.Select(static i => i.EntidadeId)];
            if (ids.Any(static id => string.IsNullOrWhiteSpace(id)))
            {
                return Result<ResultadoResolucaoArvore>.Failure(new DomainError(
                    "ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida",
                    $"Uma instância de {tipoEntidade} tem EntidadeId vazio ou em branco."));
            }

            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            {
                return Result<ResultadoResolucaoArvore>.Failure(new DomainError(
                    "ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida",
                    $"Duas ou mais instâncias de {tipoEntidade} declaram o mesmo EntidadeId — cada instância precisa de identidade única."));
            }
        }

        Dictionary<Guid, EstadoSatisfacao> estados = [];
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia = [];
        List<ConsequenciaEmitida> consequenciasVigentes = [];
        List<PendenciaDeOrientacao> pendenciasDeOrientacao = [];
        Dictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo = [];

        foreach (NoExigencia raiz in arvore.Raizes)
        {
            EstadoSatisfacao estadoRaiz = ResolverNo(
                raiz, fatosResolvidos, apresentacoesPorExigenciaId, instancias, estados, statusPorExigencia, instanciasResolvidasPorNo);
            EmitirFronteira(raiz, estadoRaiz, estados, consequenciasVigentes, pendenciasDeOrientacao, instanciasResolvidasPorNo);
        }

        return Result<ResultadoResolucaoArvore>.Success(new ResultadoResolucaoArvore(
            estados, statusPorExigencia, consequenciasVigentes, pendenciasDeOrientacao));
    }

    private static EstadoSatisfacao ResolverNo(
        NoExigencia no,
        IReadOnlyDictionary<string, JsonElement> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instanciasPorTipoEntidade,
        Dictionary<Guid, EstadoSatisfacao> estados,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia,
        Dictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        EstadoSatisfacao estado = no.RepetePorEntidade is { } tipoEntidade
            ? ResolverNoRepetido(no, tipoEntidade, fatos, apresentacoes, instanciasPorTipoEntidade, instanciasResolvidasPorNo)
            : no.Tipo == TipoNo.Folha
                ? ResolverFolha(no, fatos, apresentacoes, statusPorExigencia)
                : ResolverGrupo(no, fatos, apresentacoes, instanciasPorTipoEntidade, estados, statusPorExigencia, instanciasResolvidasPorNo);

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
        TipoEntidade tipoEntidade,
        IReadOnlyDictionary<string, JsonElement> fatosCandidato,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instanciasPorTipoEntidade,
        Dictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        IReadOnlyList<InstanciaEntidade> instancias = instanciasPorTipoEntidade.GetValueOrDefault(tipoEntidade, []);
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
            Dictionary<string, JsonElement> fatosDaInstancia = MesclarFatosDeEntidade(fatosCandidato, instancia.Atributos);
            IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoesDaInstancia =
                FiltrarApresentacoesPorEntidade(apresentacoes, instancia.EntidadeId);

            Dictionary<Guid, EstadoSatisfacao> estadosDaInstancia = [];
            Dictionary<Guid, StatusResolucaoExigencia> statusDaInstancia = [];
            // Aninhamento já é recusado no cadastro (NoExigencia.CriarGrupo) — nenhum
            // descendente aqui dentro pode ser, ele mesmo, repetePorEntidade; um dicionário
            // vazio é seguro (nunca populado nesta subárvore).
            Dictionary<Guid, List<InstanciaResolvida>> semRepeticaoAninhada = [];
            EstadoSatisfacao estadoInstancia = no.Tipo == TipoNo.Folha
                ? ResolverFolha(no, fatosDaInstancia, apresentacoesDaInstancia, statusDaInstancia)
                : ResolverGrupo(
                    no, fatosDaInstancia, apresentacoesDaInstancia, instanciasPorTipoEntidade,
                    estadosDaInstancia, statusDaInstancia, semRepeticaoAninhada);

            resolvidas.Add(new InstanciaResolvida(instancia.EntidadeId, estadoInstancia, estadosDaInstancia));
        }

        instanciasResolvidasPorNo[no.Id] = resolvidas;

        // Agregação entre instâncias: a subárvore só está satisfeita quando TODAS as instâncias
        // declaradas satisfazem — mesma álgebra do E (o ramo "todas não-aplicáveis" não ocorre
        // aqui na prática, já tratado como caso "zero instâncias" acima; ResolverE cobre por
        // defesa em profundidade mesmo assim).
        return ResolverE([.. resolvidas.Select(static r => r.Estado)]);
    }

    private static Dictionary<string, JsonElement> MesclarFatosDeEntidade(
        IReadOnlyDictionary<string, JsonElement> fatosCandidato, IReadOnlyDictionary<string, JsonElement> atributosDaInstancia)
    {
        Dictionary<string, JsonElement> mesclado = new(fatosCandidato, StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonElement> atributo in atributosDaInstancia)
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

    private static EstadoSatisfacao ResolverFolha(
        NoExigencia no,
        IReadOnlyDictionary<string, JsonElement> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia)
    {
        DocumentoExigido folha = no.DocumentoExigido!;
        Ternario aplicavel = folha.AplicavelPara(fatos);

        // Story #916 (herdado): Indeterminado é resolvido fato-a-fato, SEMPRE — independente de já
        // existir apresentação (nunca vira Satisfeita só porque o candidato enviou algo).
        if (aplicavel == Ternario.Indeterminado)
        {
            statusPorExigencia[folha.Id] = StatusResolucaoExigencia.AplicabilidadeIndeterminada;
            return EstadoSatisfacao.Indeterminado;
        }

        if (aplicavel == Ternario.Falso)
        {
            statusPorExigencia[folha.Id] = StatusResolucaoExigencia.NaoAplicavel;
            return EstadoSatisfacao.NaoAplicavel;
        }

        // Story #921 — cardinalidade qualificada: quantidadeMinima conta apresentações (não
        // arquivos), qualificadas por chaveDistincao quando a folha declara slots (calendário
        // derivável ou ocorrências concretas) ou só distinção pura (Ocorrencia sem lista).
        int quantidadeMinima = no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao;
        // Dedup por IDENTIDADE uma vez, para os três modos — a mesma apresentação relatada
        // duas vezes (mesmo Id, ex.: join/merge duplicado a montante) nunca pode contribuir
        // duas vezes: nem para a contagem bruta, nem para duas tags de distinção pura, nem
        // para cobrir dois slots concretos diferentes (uma apresentação = um slot).
        List<ApresentacaoDocumento> apresentacoesDaFolha =
            [.. apresentacoes.GetValueOrDefault(folha.Id, []).DistinctBy(static a => a.Id)];
        bool satisfeita = no.ChaveDistincao switch
        {
            null => apresentacoesDaFolha.Count >= quantidadeMinima,
            // Distinção pura: tags nulas/em branco não distinguem nada — ignoradas antes do
            // Distinct, senão [null, "x"] contaria como 2 tags diferentes.
            ChaveDistincao.Ocorrencia when no.OcorrenciasEsperadas is null =>
                apresentacoesDaFolha
                    .Select(static a => a.ChaveDistincao)
                    .Where(static chave => !string.IsNullOrWhiteSpace(chave))
                    .Distinct(StringComparer.Ordinal)
                    .Count() >= quantidadeMinima,
            _ => no.SlotsEsperados()!.All(slot =>
                apresentacoesDaFolha.Any(a => a.ChaveDistincao == slot)),
        };

        if (satisfeita)
        {
            statusPorExigencia[folha.Id] = StatusResolucaoExigencia.Satisfeita;
            return EstadoSatisfacao.Satisfeito;
        }

        statusPorExigencia[folha.Id] = StatusResolucaoExigencia.Pendente;
        return EstadoSatisfacao.Pendente;
    }

    private static EstadoSatisfacao ResolverGrupo(
        NoExigencia no,
        IReadOnlyDictionary<string, JsonElement> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlyDictionary<TipoEntidade, IReadOnlyList<InstanciaEntidade>> instanciasPorTipoEntidade,
        Dictionary<Guid, EstadoSatisfacao> estados,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia,
        Dictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        List<EstadoSatisfacao> estadosFilhos = [.. no.Filhos.Select(filho =>
            ResolverNo(filho, fatos, apresentacoes, instanciasPorTipoEntidade, estados, statusPorExigencia, instanciasResolvidasPorNo))];

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

    /// <summary>
    /// Emissão de consequência por fronteira ativa, raiz→baixo — nunca pai e filho emitindo ao
    /// mesmo tempo. Chamada uma vez por raiz de <see cref="ArvoreExigenciasCongelada.Raizes"/>.
    /// Dispatcher: se <paramref name="no"/> é a raiz de uma subárvore repetida (Story #922),
    /// desdobra por instância (cada uma com seu próprio dicionário de estados descendentes,
    /// nunca o global) antes de aplicar a regra normal de fronteira.
    /// </summary>
    private static void EmitirFronteira(
        NoExigencia no,
        EstadoSatisfacao estado,
        IReadOnlyDictionary<Guid, EstadoSatisfacao> estados,
        List<ConsequenciaEmitida> consequenciasVigentes,
        List<PendenciaDeOrientacao> pendenciasDeOrientacao,
        IReadOnlyDictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        if (estado is EstadoSatisfacao.Satisfeito or EstadoSatisfacao.NaoAplicavel)
        {
            // Nó satisfeito/não-aplicável suprime toda a subárvore — nada abaixo vigora.
            return;
        }

        if (no.RepetePorEntidade is not null)
        {
            // Cada instância pendente/indeterminada/impossível é uma obrigação DISTINTA do
            // candidato (ex.: "PJ 2 ainda deve os extratos") — emite por instância, tagueada com
            // entidade_id (consequências E orientações, senão duas instâncias com filhos
            // pendentes diferentes de um MESMO grupo OU repetido ficariam indistinguíveis numa
            // lista só), usando os estados DESCENDENTES resolvidos para aquela instância (nunca
            // o dicionário global, que não os contém).
            foreach (InstanciaResolvida instancia in instanciasResolvidasPorNo.GetValueOrDefault(no.Id, []))
            {
                if (!EhPendenteOuPior(instancia.Estado))
                {
                    continue;
                }

                List<ConsequenciaEmitida> consequenciasDaInstancia = [];
                List<PendenciaDeOrientacao> pendenciasDaInstancia = [];
                EmitirFronteiraNormal(
                    no, instancia.Estado, instancia.EstadosDescendentes,
                    consequenciasDaInstancia, pendenciasDaInstancia, instanciasResolvidasPorNo);

                consequenciasVigentes.AddRange(
                    consequenciasDaInstancia.Select(c => c with { EntidadeId = instancia.EntidadeId }));
                pendenciasDeOrientacao.AddRange(
                    pendenciasDaInstancia.Select(p => p with { EntidadeId = instancia.EntidadeId }));
            }

            return;
        }

        EmitirFronteiraNormal(no, estado, estados, consequenciasVigentes, pendenciasDeOrientacao, instanciasResolvidasPorNo);
    }

    private static void EmitirFronteiraNormal(
        NoExigencia no,
        EstadoSatisfacao estado,
        IReadOnlyDictionary<Guid, EstadoSatisfacao> estados,
        List<ConsequenciaEmitida> consequenciasVigentes,
        List<PendenciaDeOrientacao> pendenciasDeOrientacao,
        IReadOnlyDictionary<Guid, List<InstanciaResolvida>> instanciasResolvidasPorNo)
    {
        switch (no.Tipo)
        {
            case TipoNo.Folha:
                if (no.DocumentoExigido!.ConsequenciaIndeferimento is { } consequenciaFolha)
                {
                    consequenciasVigentes.Add(new ConsequenciaEmitida(no.Id, TipoNo.Folha, consequenciaFolha));
                }

                break;

            case TipoNo.GrupoOu:
                if (no.Consequencia is { } consequenciaGrupo)
                {
                    consequenciasVigentes.Add(new ConsequenciaEmitida(no.Id, TipoNo.GrupoOu, consequenciaGrupo));
                }

                // Opaco: os filhos NÃO emitem consequência individual — só orientação.
                foreach (NoExigencia filho in no.Filhos.Where(filho => EhPendenteOuPior(estados[filho.Id])))
                {
                    pendenciasDeOrientacao.Add(new PendenciaDeOrientacao(filho.Id));
                }

                break;

            case TipoNo.GrupoE:
                // Transparente: não emite consequência própria — desce para os filhos não
                // satisfeitos e não-não-aplicáveis, cada um emitindo pela sua regra (via
                // EmitirFronteira, não EmitirFronteiraNormal — um filho pode, ele mesmo, ser a
                // raiz de uma subárvore repetida).
                foreach (NoExigencia filho in no.Filhos)
                {
                    EstadoSatisfacao estadoFilho = estados[filho.Id];
                    if (EhPendenteOuPior(estadoFilho))
                    {
                        EmitirFronteira(filho, estadoFilho, estados, consequenciasVigentes, pendenciasDeOrientacao, instanciasResolvidasPorNo);
                    }
                }

                break;
        }
    }

    private static bool EhPendenteOuPior(EstadoSatisfacao estado) =>
        estado is EstadoSatisfacao.Pendente or EstadoSatisfacao.Indeterminado or EstadoSatisfacao.Impossivel;

    /// <summary>
    /// Story #922 — defesa em profundidade: este nó é <c>repetePorEntidade</c> E algum
    /// descendente (a qualquer profundidade) TAMBÉM é — o mesmo invariante que
    /// <c>NoExigencia.CriarGrupo</c> já recusa na ESCRITA, revalidado aqui porque
    /// <c>NoExigencia.Reidratar</c> confia no dado congelado sem revalidar.
    /// </summary>
    private static bool ContemRepeticaoAninhada(NoExigencia no) =>
        (no.RepetePorEntidade is not null && no.Filhos.Any(ContemRepeticaoOuDescendente))
        || no.Filhos.Any(ContemRepeticaoAninhada);

    private static bool ContemRepeticaoOuDescendente(NoExigencia no) =>
        no.RepetePorEntidade is not null || no.Filhos.Any(ContemRepeticaoOuDescendente);

    /// <summary>O estado resolvido de UMA instância de entidade (Story #922), com seus próprios estados descendentes — nunca compartilhado com outra instância nem com o dicionário global.</summary>
    private sealed record InstanciaResolvida(string EntidadeId, EstadoSatisfacao Estado, Dictionary<Guid, EstadoSatisfacao> EstadosDescendentes);
}
