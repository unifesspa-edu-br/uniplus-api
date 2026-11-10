namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// O que a decisão produziu: uma liberação por concessão decidida, as bases novas das que ficaram com todas as etapas
/// exigidas liberadas, e as concessões que a decisão em lote deixou de fora.
/// </summary>
/// <param name="Liberacoes">Uma por concessão decidida.</param>
/// <param name="Bases">A base nova das concessões cujas etapas exigidas estão todas liberadas.</param>
/// <param name="ParaOutraPessoa">As concessões do próprio decisor ou que ele já decidiu em outra etapa; ficam para outra pessoa.</param>
/// <param name="PuladasPorEscopo">As concessões cujo escopo não comporta o novo alcance; só voltam por pedido novo.</param>
public sealed record DesfechoDaDecisaoDaReclassificacao(
    IReadOnlyList<LiberacaoDaReclassificacao> Liberacoes,
    IReadOnlyList<BaseDaPermissaoNaConcessao> Bases,
    IReadOnlyList<Guid> ParaOutraPessoa,
    IReadOnlyList<Guid> PuladasPorEscopo);
