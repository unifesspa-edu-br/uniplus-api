namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// O que o percurso das raízes da árvore de satisfação acumula: os estados, os status, as consequências
/// vigentes e as orientações, e o resultado agregado, inclusive o status e o estado por ocorrência de cada
/// subárvore repetida. Quem resolve os nós grava nas coleções e fecha cada raiz com a máscara e a emissão.
/// </summary>
internal sealed class PercursoDaArvore(IReadOnlySet<Guid> bloqueadas)
{
    private readonly List<ConsequenciaEmitida> _consequenciasVigentes = [];
    private readonly List<PendenciaDeOrientacao> _pendenciasDeOrientacao = [];
    private readonly HashSet<Guid> _nosEmissaoSuprimida = [];

    internal Dictionary<Guid, EstadoSatisfacao> Estados { get; } = [];

    internal Dictionary<Guid, StatusResolucaoExigencia> StatusPorExigencia { get; } = [];

    internal Dictionary<Guid, List<InstanciaResolvida>> InstanciasResolvidasPorNo { get; } = [];

    internal void Fechar(NoExigencia raiz, EstadoSatisfacao estadoRaiz)
    {
        // A máscara emissionBlocked é resolvida DEPOIS que a subárvore inteira tem estado — o
        // predicado de um grupo depende do de todos os seus filhos decisivos.
        FronteiraDeEmissao.CalcularEmissionBlocked(raiz, Estados, bloqueadas, _nosEmissaoSuprimida);
        FronteiraDeEmissao.Emitir(
            raiz, estadoRaiz, Estados, _nosEmissaoSuprimida, _consequenciasVigentes, _pendenciasDeOrientacao, InstanciasResolvidasPorNo);
    }

    internal ResultadoResolucaoArvore Resultado() => new(
        Estados, StatusPorExigencia, _consequenciasVigentes, _pendenciasDeOrientacao, StatusPorEntidade(), EstadosPorEntidade(), _nosEmissaoSuprimida);

    // Sem este achatamento, uma folha Obrigatorio sem ConsequenciaIndeferimento configurada (que nunca
    // emite ConsequenciaEmitida) só apareceria pendente no AGREGADO da raiz repetida, sem sinalizar QUAL
    // entidade_id ainda deve o documento.
    private List<StatusPorEntidade> StatusPorEntidade() => [.. InstanciasResolvidasPorNo.Values
        .SelectMany(static resolvidas => resolvidas)
        .SelectMany(static instancia => instancia.StatusDescendentes.Select(
            par => new StatusPorEntidade(par.Key, instancia.EntidadeId, par.Value)))];

    private List<EstadoPorEntidade> EstadosPorEntidade() => [.. InstanciasResolvidasPorNo.Values
        .SelectMany(static resolvidas => resolvidas)
        .SelectMany(static instancia => instancia.EstadosDescendentes.Select(
            par => new EstadoPorEntidade(par.Key, instancia.EntidadeId, par.Value)))];
}
