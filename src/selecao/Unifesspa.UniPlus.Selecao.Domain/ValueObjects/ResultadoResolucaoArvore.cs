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
/// <param name="EntidadeId">
/// Story #922 — quando <see cref="NoExigenciaId"/> está dentro de (ou é) uma subárvore
/// <c>repetePorEntidade</c>, o <c>entidade_id</c> da instância cuja pendência esta consequência
/// descreve (ex.: "PJ 2 ainda deve os extratos"). <see langword="null"/> fora de subárvore
/// repetida.
/// </param>
public sealed record ConsequenciaEmitida(Guid NoExigenciaId, TipoNo TipoOrigem, string Consequencia, string? EntidadeId = null);

/// <summary>
/// Um filho DIRETO de um grupo <c>OU</c>/<c>N-de</c> opaco pendente/indeterminado/impossível —
/// listado apenas como orientação (não vigente, o grupo pai é quem emite a consequência).
/// </summary>
/// <param name="NoExigenciaId">O Id do nó filho.</param>
/// <param name="EntidadeId">
/// Story #922 — quando o grupo opaco está dentro de (ou é) uma subárvore
/// <c>repetePorEntidade</c>, o <c>entidade_id</c> da instância a que esta orientação se refere
/// (sem isto, orientações de instâncias diferentes de um mesmo nó ficariam indistinguíveis
/// numa lista só). <see langword="null"/> fora de subárvore repetida.
/// </param>
public sealed record PendenciaDeOrientacao(Guid NoExigenciaId, string? EntidadeId = null);

/// <summary>
/// Story #922 — o <see cref="StatusResolucaoExigencia"/> de UMA folha PARA UMA instância
/// específica, dentro de uma subárvore <c>repetePorEntidade</c>. Complementa
/// <see cref="ResultadoResolucaoArvore.StatusPorExigencia"/> (que só cobre folhas fora de
/// repetição): sem isto, uma folha <c>Obrigatorio</c> sem <c>ConsequenciaIndeferimento</c>
/// configurada (não emite <see cref="ConsequenciaEmitida"/>) ficaria com pendência visível
/// só no AGREGADO da raiz repetida — nenhum sinal de QUAL <c>entidade_id</c> ainda deve o
/// documento.
/// </summary>
/// <param name="DocumentoExigidoId">A exigência — mesma chave de <see cref="ResultadoResolucaoArvore.StatusPorExigencia"/>.</param>
/// <param name="EntidadeId">A instância a que este status se refere.</param>
/// <param name="Status">O status desta folha PARA esta instância.</param>
public sealed record StatusPorEntidade(Guid DocumentoExigidoId, string EntidadeId, StatusResolucaoExigencia Status);

/// <summary>
/// O resultado agregado de <see cref="Services.ResolvedorArvoreSatisfacao.Resolver"/> (Story #920)
/// — substitui <c>ResultadoResolucaoExigencias</c> (grupo plano).
/// </summary>
/// <param name="EstadosPorNo">
/// O <see cref="EstadoSatisfacao"/> de todo nó FORA de uma subárvore <c>repetePorEntidade</c>
/// (Story #922), chave = <c>NoExigencia.Id</c> — inclui o AGREGADO da própria raiz de uma
/// subárvore repetida (satisfeita só quando TODAS as instâncias declaradas satisfazem, mesma
/// álgebra do <c>E</c>), mas não os descendentes dela: o estado é inerentemente por-instância
/// dentro de uma repetição, então não há um único valor global útil para eles — o detalhe
/// por-instância vigora em <see cref="ConsequenciasVigentes"/>, cada uma tagueada com
/// <see cref="ConsequenciaEmitida.EntidadeId"/>.
/// </param>
/// <param name="StatusPorExigencia">
/// Projeção por FOLHA (chave = <c>DocumentoExigido.Id</c>) — paridade com o formato anterior;
/// folha nunca resolve <see cref="EstadoSatisfacao.Impossivel"/> (só emerge em nó de grupo pela
/// cardinalidade). MESMA ressalva de <see cref="EstadosPorNo"/>: folhas dentro de uma subárvore
/// <c>repetePorEntidade</c> não entram aqui (múltiplas instâncias, uma chave só) — o mapeamento é
/// total apenas para folhas fora de repetição.
/// </param>
/// <param name="ConsequenciasVigentes">As consequências emitidas pela fronteira ativa — as que REALMENTE vigoram (sem dupla emissão, sem ramo já satisfeito). Dentro de uma subárvore repetida, uma por INSTÂNCIA pendente (Story #922).</param>
/// <param name="PendenciasDeOrientacao">Os filhos diretos de grupos <c>OU</c>/<c>N-de</c> opacos pendentes/indeterminados/impossíveis — ver <see cref="PendenciaDeOrientacao"/>.</param>
/// <param name="StatusPorEntidade">
/// Story #922 — o status de TODA folha dentro de QUALQUER subárvore <c>repetePorEntidade</c>,
/// uma entrada por (folha, instância) — o complemento de <see cref="StatusPorExigencia"/> que
/// este NÃO cobre. Vazio quando a árvore não usa repetição.
/// </param>
/// <param name="NosEmissaoSuprimida">
/// Story #928, §6 — os ids dos nós cuja emissão está suprimida pela fronteira de disponibilidade
/// (<c>emissionBlocked</c>): folha BLOQUEADA ou grupo cuja fronteira decisiva é toda bloqueada. Um
/// nó aqui projeta <see cref="EstadoSatisfacao.Indeterminado"/> na agregação, mas não emite
/// consequência nem orientação — distingue "bloqueado" (não pedir) de "indeterminado no ponto
/// devido" (pode emitir), que compartilham o mesmo estado. Vazio quando não há fronteira a mascarar.
/// </param>
public sealed record ResultadoResolucaoArvore(
    IReadOnlyDictionary<Guid, EstadoSatisfacao> EstadosPorNo,
    IReadOnlyDictionary<Guid, StatusResolucaoExigencia> StatusPorExigencia,
    IReadOnlyList<ConsequenciaEmitida> ConsequenciasVigentes,
    IReadOnlyList<PendenciaDeOrientacao> PendenciasDeOrientacao,
    IReadOnlyList<StatusPorEntidade> StatusPorEntidade,
    IReadOnlySet<Guid> NosEmissaoSuprimida);
