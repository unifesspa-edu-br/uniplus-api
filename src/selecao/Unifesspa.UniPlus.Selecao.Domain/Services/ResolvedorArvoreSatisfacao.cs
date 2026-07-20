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
/// </list>
/// </remarks>
public static class ResolvedorArvoreSatisfacao
{
    public static Result<ResultadoResolucaoArvore> Resolver(
        ArvoreExigenciasCongelada? arvore,
        IReadOnlyDictionary<string, JsonElement> fatosResolvidos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoesPorExigenciaId)
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

        Dictionary<Guid, EstadoSatisfacao> estados = [];
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia = [];
        List<ConsequenciaEmitida> consequenciasVigentes = [];
        List<Guid> pendenciasDeOrientacao = [];

        foreach (NoExigencia raiz in arvore.Raizes)
        {
            EstadoSatisfacao estadoRaiz = ResolverNo(raiz, fatosResolvidos, apresentacoesPorExigenciaId, estados, statusPorExigencia);
            EmitirFronteira(raiz, estadoRaiz, estados, consequenciasVigentes, pendenciasDeOrientacao);
        }

        return Result<ResultadoResolucaoArvore>.Success(new ResultadoResolucaoArvore(
            estados, statusPorExigencia, consequenciasVigentes, pendenciasDeOrientacao));
    }

    private static EstadoSatisfacao ResolverNo(
        NoExigencia no,
        IReadOnlyDictionary<string, JsonElement> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        Dictionary<Guid, EstadoSatisfacao> estados,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia)
    {
        EstadoSatisfacao estado = no.Tipo == TipoNo.Folha
            ? ResolverFolha(no, fatos, apresentacoes, statusPorExigencia)
            : ResolverGrupo(no, fatos, apresentacoes, estados, statusPorExigencia);

        estados[no.Id] = estado;
        return estado;
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
        Dictionary<Guid, EstadoSatisfacao> estados,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia)
    {
        List<EstadoSatisfacao> estadosFilhos = [.. no.Filhos.Select(filho =>
            ResolverNo(filho, fatos, apresentacoes, estados, statusPorExigencia))];

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
    /// </summary>
    private static void EmitirFronteira(
        NoExigencia no,
        EstadoSatisfacao estado,
        IReadOnlyDictionary<Guid, EstadoSatisfacao> estados,
        List<ConsequenciaEmitida> consequenciasVigentes,
        List<Guid> pendenciasDeOrientacao)
    {
        if (estado is EstadoSatisfacao.Satisfeito or EstadoSatisfacao.NaoAplicavel)
        {
            // Nó satisfeito/não-aplicável suprime toda a subárvore — nada abaixo vigora.
            return;
        }

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
                    pendenciasDeOrientacao.Add(filho.Id);
                }

                break;

            case TipoNo.GrupoE:
                // Transparente: não emite consequência própria — desce para os filhos não
                // satisfeitos e não-não-aplicáveis, cada um emitindo pela sua regra.
                foreach (NoExigencia filho in no.Filhos)
                {
                    EstadoSatisfacao estadoFilho = estados[filho.Id];
                    if (EhPendenteOuPior(estadoFilho))
                    {
                        EmitirFronteira(filho, estadoFilho, estados, consequenciasVigentes, pendenciasDeOrientacao);
                    }
                }

                break;
        }
    }

    private static bool EhPendenteOuPior(EstadoSatisfacao estado) =>
        estado is EstadoSatisfacao.Pendente or EstadoSatisfacao.Indeterminado or EstadoSatisfacao.Impossivel;
}
