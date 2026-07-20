namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Enums;

/// <summary>
/// Uma consequência VIGENTE emitida pela fronteira ativa (Story #920) — <see cref="NoExigenciaId"/>
/// é sempre o Id do <b>nó</b> (folha ou grupo <c>OU</c>/<c>N-de</c>) que a carrega, nunca o
/// <c>DocumentoExigidoId</c> (a correlação exigência↔apresentação continua sendo
/// <c>DocumentoExigido.Id</c>, dado independente do Id do nó folha que o envolve).
/// </summary>
/// <param name="NoExigenciaId">O Id do nó que emitiu — folha ou grupo <c>OU</c>/<c>N-de</c>.</param>
/// <param name="TipoOrigem"><see cref="TipoNo.Folha"/> ou <see cref="TipoNo.GrupoOu"/> — nunca <see cref="TipoNo.GrupoE"/> (transparente, nunca emite consequência própria).</param>
/// <param name="Consequencia">∈ {ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM, PENDENCIA_REENVIO}.</param>
public sealed record ConsequenciaEmitida(Guid NoExigenciaId, TipoNo TipoOrigem, string Consequencia);

/// <summary>
/// O resultado agregado de <see cref="Services.ResolvedorArvoreSatisfacao.Resolver"/> (Story #920)
/// — substitui <c>ResultadoResolucaoExigencias</c> (grupo plano).
/// </summary>
/// <param name="EstadosPorNo">O <see cref="EstadoSatisfacao"/> de TODO nó da árvore (folha e grupo), chave = <c>NoExigencia.Id</c>.</param>
/// <param name="StatusPorExigencia">
/// Projeção por FOLHA (chave = <c>DocumentoExigido.Id</c>) — paridade com o formato anterior;
/// folha nunca resolve <see cref="EstadoSatisfacao.Impossivel"/> (só emerge em nó de grupo pela
/// cardinalidade), então o mapeamento é total.
/// </param>
/// <param name="ConsequenciasVigentes">As consequências emitidas pela fronteira ativa — as que REALMENTE vigoram (sem dupla emissão, sem ramo já satisfeito).</param>
/// <param name="PendenciasDeOrientacao">
/// Ids de nó dos filhos DIRETOS de um grupo <c>OU</c>/<c>N-de</c> opaco pendente/indeterminado/impossível
/// — listados apenas como orientação (não vigentes, o grupo pai é quem emite).
/// </param>
public sealed record ResultadoResolucaoArvore(
    IReadOnlyDictionary<Guid, EstadoSatisfacao> EstadosPorNo,
    IReadOnlyDictionary<Guid, StatusResolucaoExigencia> StatusPorExigencia,
    IReadOnlyList<ConsequenciaEmitida> ConsequenciasVigentes,
    IReadOnlyList<Guid> PendenciasDeOrientacao);
